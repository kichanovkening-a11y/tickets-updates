"""Build the approved TTN's editable Word grid from the exact supplied XLS.

Run with the primary runtime Python (xlrd, python-docx, lxml):
  build_approved_ttn.py source.xls native-geometry.json output.docx
Use --reference only for a private QA copy with the source's sample values.
The source is read-only. The shipped template contains fields, not sample parties.
"""
import argparse
import hashlib
import json
import re
from copy import deepcopy
from pathlib import Path
from zipfile import ZipFile, ZIP_DEFLATED

import xlrd
from docx import Document
from lxml import etree as E

SOURCE_SHA256 = "327ea193b894edcdc7e653dcb8f2bf62cd3c57ceab9e1e046c74be387067fe82"
W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main"
REL = "http://schemas.openxmlformats.org/package/2006/relationships"


def node(tag, **attrs):
    return E.Element(f"{{{W}}}{tag}", {f"{{{W}}}{k}": str(v) for k, v in attrs.items()})


def cell_address(row, col):
    return xlrd.formula.cellname(row, col)


def replacements():
    values = {
        "A7": "{senderDetailsInline}", "I14": "{senderDetailsInline}",
        "L12": "{recipientDetailsInline}", "I16": "{recipientDetailsInline}",
        "I18": "Договор", "CF17": "{contractNumber}", "CF19": "{contractDate}",
        "AX26": "{issueId}", "BI26": "{date}", "D31": "{ttnDescription}",
        "Y47": "{ttnAttachmentPages}", "K48": "{lineCountWords}", "K54": "{countWords}",
        "N59": "{amountWords}", "L63": "{senderPosition}", "AG63": "{senderSignatory}",
        "AG65": "", "L67": "{senderPosition}", "AG67": "{senderSignatory}",
        "BN67": "{recipientPosition}", "CF67": "{recipientSignatory}",
        "N70": "{ttnDay}", "R70": "{ttnMonth}", "AA70": "{ttnYear}",
        "BE70": "{ttnDay}", "BI70": "{ttnMonth}", "BR70": "{ttnYear}",
    }
    for index, row in enumerate((31, 32, 33, 40, 41, 42, 43), 1):
        for col, key in (("A", "Number"), ("X", "Unit"), ("BB", "Count"), ("BH", "Price"), ("BQ", "Total"), ("CI", "Total")):
            values[f"{col}{row}"] = "{ttnItem" + key + str(index) + "}"
        if index > 3:
            values[f"D{row}"] = "{ttnItemDescription" + str(index) + "}"
    for row, name in ((34, "Front"), (44, "Back"), (45, "All")):
        values[f"BB{row}"] = "{ttn" + name + "Count}"
        for col in ("BQ", "CI"):
            values[f"{col}{row}"] = "{ttn" + name + "Total}"
    return values


def build(source, geometry, target, reference=False):
    if hashlib.sha256(source.read_bytes()).hexdigest() != SOURCE_SHA256:
        raise ValueError("This is not the approved XLS supplied on 1 November")
    book = xlrd.open_workbook(str(source), formatting_info=True)
    sheet = book.sheet_by_index(0)
    sizes = json.loads(geometry.read_text(encoding="utf-8-sig"))
    if sizes["PrintArea"] != "$A$1:$CQ$71" or sizes["Orientation"] != 2:
        raise ValueError("Reference must be the approved two-sided landscape print area")
    fields = {} if reference else replacements()
    merges = list(sheet.merged_cells)
    occupied = {}
    for area in merges:
        r0, r1, c0, c1 = area
        for r in range(r0, r1):
            for c in range(c0, c1):
                occupied[r, c] = area

    def original(r, c):
        return sheet.cell(r, c) if r < sheet.nrows else xlrd.sheet.Cell(xlrd.XL_CELL_EMPTY, "", 15)

    def format_at(r, c):
        return book.xf_list[original(r, c).xf_index]

    def border(r, c, edge):
        return getattr(format_at(r, c).border, edge + "_line_style")

    # Excel lets unmerged labels spill into adjacent empty borderless cells.
    # Give those labels the same visible span in Word (not a narrow clipped cell).
    virtual = dict(occupied)
    for r in range(sheet.nrows):
        for c in range(sheet.ncols):
            cell = sheet.cell(r, c)
            xf = format_at(r, c)
            if not str(cell.value) or (r, c) in virtual or xf.alignment.text_wrapped:
                continue
            lo, hi = c, c + 1
            if xf.alignment.hor_align == 3:
                while lo > 0 and (r, lo - 1) not in virtual and sheet.cell_value(r, lo - 1) == "" and not border(r, lo, "left") and not border(r, lo - 1, "right"):
                    lo -= 1
            elif xf.alignment.hor_align in (0, 1):
                while hi < 95 and (r, hi) not in virtual and sheet.cell_value(r, hi) == "" and not border(r, hi - 1, "right") and not border(r, hi, "left"):
                    hi += 1
            if hi - lo > 1:
                area = (r, r + 1, lo, hi)
                for column in range(lo, hi):
                    virtual[r, column] = area

    def text(r, c):
        address = cell_address(r, c)
        if address in fields:
            return fields[address]
        if r >= sheet.nrows:
            return ""
        cell = sheet.cell(r, c)
        if cell.ctype == xlrd.XL_CELL_NUMBER:
            fmt = book.format_map[format_at(r, c).format_key].format_str
            return (f"{cell.value:.2f}".replace(".", ",") if "0.00" in fmt else f"{cell.value:g}")
        return str(cell.value)

    def cell_origin(area):
        r0, _, c0, c1 = area
        # A right-aligned spilled label's source is at the right end.
        return next(((r0, c) for c in range(c0, c1) if (r0 < sheet.nrows and sheet.cell_value(r0, c) != "") or cell_address(r0, c) in fields), (r0, c0))

    def rows(begin, end):
        result = []
        for r in range(begin, end):
            cells = []
            c = 0
            while c < 95:
                area = virtual.get((r, c), (r, r + 1, c, c + 1))
                r0, r1, c0, c1 = area
                if c != c0:
                    raise ValueError("Overlapping source merges")
                # Coalesce only empty unmerged spacers with no internal edge.
                if (r, c) not in virtual and text(r, c) == "":
                    while c1 < 95 and (r, c1) not in virtual and text(r, c1) == "" and not border(r, c1 - 1, "right") and not border(r, c1, "left") and all(border(r, c1, side) == border(r, c, side) for side in ("top", "bottom")):
                        c1 += 1
                    area = (r, r + 1, c, c1)
                cells.append(area)
                c = c1
            result.append((r, cells))
        return result

    def font_props(font):
        props = node("rPr")
        props.append(node("rFonts", ascii=font.name, hAnsi=font.name, cs=font.name))
        if font.bold:
            props.append(node("b"))
        if font.italic:
            props.append(node("i"))
        props.append(node("color", val="000000"))
        props.append(node("sz", val=round(font.height / 10)))
        props.append(node("szCs", val=round(font.height / 10)))
        if font.underlined:
            props.append(node("u", val="single"))
        return props

    bookmark_id = 100
    def paragraph(r, c, value, mark=True):
        nonlocal bookmark_id
        xf = format_at(r, c)
        font_index = sheet.rich_text_runlist_map.get((r, c), [(0, xf.font_index)])[0][1]
        font = book.font_list[font_index]
        p = node("p")
        pr = node("pPr")
        pr.append(node("keepNext", val=0))
        pr.append(node("widowControl", val=0))
        pr.append(node("snapToGrid", val=0))
        pr.append(node("spacing", before=0, after=0, line=240, lineRule="auto"))
        alignment = {1: "left", 2: "center", 3: "right", 4: "both", 7: "distribute"}.get(xf.alignment.hor_align)
        if alignment is None:
            alignment = "right" if original(r, c).ctype == xlrd.XL_CELL_NUMBER else "left"
        pr.append(node("jc", val=alignment))
        pr.append(font_props(font))
        p.append(pr)
        address = cell_address(r, c)
        if mark and address in replacements():
            bookmark_id += 1
            p.append(node("bookmarkStart", id=bookmark_id, name="TtnCell_" + address))
        ranges = sheet.rich_text_runlist_map.get((r, c), [(0, font_index)]) if reference else [(0, font_index)]
        for n, (start, font_index) in enumerate(ranges):
            end = ranges[n + 1][0] if n + 1 < len(ranges) else len(value)
            run = node("r")
            run.append(font_props(book.font_list[font_index]))
            for i, line in enumerate(value[start:end].split("\n")):
                if i:
                    run.append(node("br"))
                t = node("t")
                t.set("{http://www.w3.org/XML/1998/namespace}space", "preserve")
                t.text = line
                run.append(t)
            p.append(run)
        if mark and address in replacements():
            p.append(node("bookmarkEnd", id=bookmark_id))
        return p

    field_geometry = {}
    def make_table(begin, end):
        source_rows = rows(begin, end)
        edges = sorted({0, 95} | {c for _, cells in source_rows for area in cells for c in area[2:]})
        if len(edges) - 1 > 63:
            raise ValueError(f"Word's 63-column limit exceeded: {len(edges)-1}")
        positions = {c: i for i, c in enumerate(edges)}
        table = node("tbl")
        pr = node("tblPr")
        pr.append(node("tblW", w=round(sum(sizes["Columns"]) * 20), type="dxa"))
        pr.append(node("jc", val="left"))
        pr.append(node("tblInd", w=39, type="dxa"))
        pr.append(node("tblLayout", type="fixed"))
        margin = node("tblCellMar")
        for name, width in (("top", 0), ("left", 30), ("bottom", 0), ("right", 30)):
            margin.append(node(name, w=width, type="dxa"))
        pr.append(margin)
        table.append(pr)
        grid = node("tblGrid")
        for lo, hi in zip(edges, edges[1:]):
            grid.append(node("gridCol", w=round(sum(sizes["Columns"][lo:hi]) * 20)))
        table.append(grid)
        for r, cells in source_rows:
            row = node("tr")
            row_props = node("trPr")
            row_props.append(node("cantSplit"))
            row_props.append(node("trHeight", val=round(sizes["Rows"][r] * 20), hRule="exact"))
            row.append(row_props)
            for area in cells:
                r0, r1, c0, c1 = area
                sr, sc = cell_origin(area)
                cell = node("tc")
                props = node("tcPr")
                props.append(node("tcW", w=round(sum(sizes["Columns"][c0:c1]) * 20), type="dxa"))
                span = positions[c1] - positions[c0]
                if span > 1:
                    props.append(node("gridSpan", val=span))
                if r1 - r0 > 1:
                    props.append(node("vMerge", **({"val": "restart"} if r == r0 else {})))
                borders = node("tcBorders")
                for side in ("top", "left", "bottom", "right"):
                    indices = range(c0, c1) if side in ("top", "bottom") else [c0 if side == "left" else c1 - 1]
                    style = max(border(r, col, side) for col in indices)
                    val, width = {1: ("single", 8), 2: ("single", 16), 3: ("dashed", 8), 4: ("dotted", 8), 5: ("single", 24), 6: ("double", 6), 7: ("dotted", 2)}.get(style, ("nil", 0))
                    borders.append(node(side, val=val, sz=width, color="000000"))
                props.append(borders)
                props.append(node("vAlign", val={0: "top", 1: "center", 2: "bottom"}.get(format_at(sr, sc).alignment.vert_align, "center")))
                cell.append(props)
                cell.append(paragraph(sr, sc, text(sr, sc) if r == r0 else "", mark=r == r0))
                address = cell_address(sr, sc)
                if r == r0 and address in replacements():
                    font_index = sheet.rich_text_runlist_map.get((sr, sc), [(0, format_at(sr, sc).font_index)])[0][1]
                    field_geometry[address] = (sum(sizes["Rows"][r0:r1]), book.font_list[font_index].height / 20)
                row.append(cell)
            table.append(row)
        return table

    def section():
        sec = node("sectPr")
        sec.append(node("pgSz", w=16838, h=11906, orient="landscape"))
        sec.append(node("pgMar", left=round(sizes["Left"] * 20), top=round(sizes["Top"] * 20), right=round(sizes["Right"] * 20), bottom=round(sizes["Bottom"] * 20), header=283, footer=283, gutter=0))
        return sec

    document = Document()
    document.core_properties.author = ""
    document.core_properties.last_modified_by = ""
    header = document.sections[0].header
    hp = header.paragraphs[0]
    hp.paragraph_format.space_before = 0
    hp.paragraph_format.space_after = 0
    from docx.enum.text import WD_ALIGN_PARAGRAPH
    from docx.shared import Pt
    hp.paragraph_format.right_indent = Pt(26.2)
    hp.alignment = WD_ALIGN_PARAGRAPH.RIGHT
    for value, bold in (("Подготовлено с использованием системы ", False), ("КонсультантПлюс", True)):
        run = hp.add_run(value)
        run.font.name = "Times New Roman"
        run.font.size = Pt(7)
        run.bold = bold
    header_ref = document.sections[0]._sectPr.find(f"{{{W}}}headerReference")
    body = document._element.body
    for child in list(body):
        body.remove(child)
    body.append(make_table(0, 34))
    separator = node("p")
    props = node("pPr")
    props.append(node("spacing", before=0, after=0, line=1, lineRule="exact"))
    first_section = section()
    first_section.insert(0, header_ref)
    props.append(first_section)
    separator.append(props)
    body.append(separator)
    body.append(make_table(34, 71))
    last_section = section()
    last_section.insert(0, deepcopy(header_ref))
    body.append(last_section)
    target.parent.mkdir(parents=True, exist_ok=True)
    document.save(target)
    with ZipFile(target) as package:
        parts = {entry.filename: package.read(entry.filename) for entry in package.infolist()}
    metadata = E.Element("{urn:tickets:approved-ttn}layout", version="045", sourceSha256=SOURCE_SHA256, firstRows="34", secondRows="37")
    for address, (height, font_size) in field_geometry.items():
        E.SubElement(metadata, "{urn:tickets:approved-ttn}field", name="TtnCell_" + address, height=str(round(height, 2)), fontSize=str(font_size))
    parts["customXml/tickets-ttn-045.xml"] = E.tostring(metadata, xml_declaration=True, encoding="UTF-8")
    relationships = E.fromstring(parts["word/_rels/document.xml.rels"])
    E.SubElement(relationships, f"{{{REL}}}Relationship", Id="approvedTtn045", Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/customXml", Target="../customXml/tickets-ttn-045.xml")
    parts["word/_rels/document.xml.rels"] = E.tostring(relationships, xml_declaration=True, encoding="UTF-8")
    with ZipFile(target, "w", ZIP_DEFLATED) as package:
        for name, data in parts.items():
            package.writestr(name, data)
    print(f"Created {target} from approved source; reference={reference}")


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("source", type=Path)
    parser.add_argument("geometry", type=Path)
    parser.add_argument("output", type=Path)
    parser.add_argument("--reference", action="store_true")
    args = parser.parse_args()
    build(args.source, args.geometry, args.output, args.reference)
