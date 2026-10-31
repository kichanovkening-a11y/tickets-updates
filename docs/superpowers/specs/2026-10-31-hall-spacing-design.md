# Hall spacing and occupied seats — 0.43

Approved 31 October: apply the collected changes after 0.42. All spacing below means actual screen pixels, including Windows scaling, and visible text ink rather than font leading.

- Right sidebar price plaques: 5 px from panel edges and 5 px inside plaques to text. Keep vertical stacking, 20%-smaller font and price focus.
- Selected price summary: 5 px at top/sides; 3 px between lines including Total.
- Selected quantity/sum: 5 px from sidebar top/sides, 3 px between lines and before recipient label.
- Navy header: shrink to 5 px above/below its existing buttons. Preserve button fonts and functions.
- Event title/date: 5 px below navy header, both hall modes.
- Hall tabs + Print/Invoice: unchanged font, compact ink + 5 px each side, one aligned row.
- Refresh and expand/restore: equal squares, same horizontal row at right; refresh left, 5 px between. Both normal and expanded modes. Remain outside hall.
- Externally occupied seats: black square, centered white circle containing black number, no hatching. Legend agrees. Status, selection, sold and internal issued seats retain their rules.
- Sidebar manual width: shared across events and hall modes, saved across restarts; cap safely at narrow window sizes without replacing the saved preference.
- Ticket PRO fetching, parsing, authorization, queues and database semantics are unchanged.
- Publish through normal update channel after checks. Never manipulate the user's original running app.
