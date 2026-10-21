namespace Tickets.Core;

public sealed record BusinessDocumentLine(string Text,bool Requisites=false);

/// <summary>Editable business forms based on the supplied contract, act, bill and goods waybill.</summary>
public static class BusinessDocumentTemplates
{
    public static (string Seller,string Buyer) PartyLabels(string kind)=>kind switch
    {
        IssueDocumentKinds.Act=>("Исполнитель","Заказчик"),
        IssueDocumentKinds.Bill=>("Поставщик и получатель платежа","Покупатель и плательщик"),
        IssueDocumentKinds.Waybill=>("Грузоотправитель и поставщик","Грузополучатель и плательщик"),
        _=>("Продавец","Покупатель")
    };
    // Layout is shared by printing and Word export, including already saved templates.
    // The editable text remains plain text; its two requisite placeholders become one row.
    public static IReadOnlyList<BusinessDocumentLine> Layout(IssueDocumentInfo document)
    {
        var lines=document.Body.Replace("\r\n","\n").Replace('\r','\n').Split('\n');
        var omitted=new HashSet<int>();var insertion=lines.Length;
        foreach(var details in new[]{document.Invoice.SenderDetails??new OrganizationDetails(Name:document.Invoice.Sender),document.Invoice.RecipientDetails??new OrganizationDetails(Name:document.Invoice.Recipient)})
        {
            var block=Requisites(details).Split('\n');
            for(var i=0;i<=lines.Length-block.Length;i++)
            {
                if(omitted.Contains(i)||!lines.Skip(i).Take(block.Length).SequenceEqual(block))continue;
                var start=i;
                if(i>0&&IsPartyHeading(lines[i-1]))start--;
                insertion=Math.Min(insertion,start);
                for(var j=start;j<i+block.Length;j++)omitted.Add(j);
            }
        }
        if(document.Kind is IssueDocumentKinds.Contract or IssueDocumentKinds.Act)
        {
            var labels=PartyLabels(document.Kind);
            var parties=new[]{(labels.Seller,document.Invoice.SenderDetails),(labels.Buyer,document.Invoice.RecipientDetails)};
            foreach(var (label,details) in parties)
            {
                var signature=$"{label}: {Missing(details?.Position??"")} ____________ / {Missing(details?.Signatory??"")} /";
                for(var i=0;i<lines.Length;i++)if(lines[i]==signature)omitted.Add(i);
            }
        }
        var result=new List<BusinessDocumentLine>();
        for(var i=0;i<=lines.Length;i++)
        {
            if(i==insertion)result.Add(new("",true));
            if(i<lines.Length&&!omitted.Contains(i))result.Add(new(lines[i]));
        }
        return result;
    }
    private static bool IsPartyHeading(string line)=>line is "Продавец" or "Покупатель" or "Реквизиты исполнителя" or "Реквизиты заказчика" or "Поставщик и получатель платежа" or "Покупатель и плательщик" or "Грузоотправитель и поставщик" or "Грузополучатель и плательщик";
    public static string LegacyDefault(string kind)=>IssueDocumentKinds.Name(kind)+" № {issueId}\nДата: {date}\nОтправитель: {sender}\nПолучатель: {recipient}\nМероприятие: {event}\nДата мероприятия: {eventDate}\nПлощадка: {venue}\n\nПередача билетов по указанной выдаче.\n\n{summary}\n{total}\n\nПередал: ____________________    Получил: ____________________";
    public static string Default(string kind)=>SourceBusinessForms.DefaultText(kind);
    public static string Version011Default(string kind)
    {
        var template=PreviousDefault(kind);
        return kind switch
        {
            IssueDocumentKinds.Contract=>template
                .Replace("1.1. Продавец обязуется передать Покупателю театральные билеты на мероприятие «{event}» на площадке {venue}. Дата и время мероприятия: {eventDate}. Количество: {count} шт. Стоимость: {amount} руб.","1.1. Продавец принимает на себя обязательства по организации и проведению культурно-массового мероприятия и продаже театральных билетов Покупателю.\nТеатральные билеты на мероприятие «{event}» на площадке {venue}. Дата и время: {eventDate}. Количество: {count} шт., общая сумма: {amount} руб. ({amountWords}).")
                .Replace("2.2. Покупатель перечисляет оплату на расчётный счёт Продавца. Размер предоплаты: ____________. Срок оплаты: ____________.","2.2. Покупатель осуществляет 100% оплату на расчётный счёт Продавца в размере {amount} руб. ({amountWords}). Срок оплаты: ____________.")
                .Replace("2.3. Срок и порядок передачи билетов: ____________.","2.3. После поступления денежных средств на расчётный счёт Продавца последний обязуется в течение 10 дней предоставить Покупателю билеты в количестве {count} шт.")
                .Replace("3.2. При отмене мероприятия по причинам, за которые отвечает Продавец, порядок и срок возврата платежей: ____________.","3.2. Если мероприятие не состоится по причинам, за которые отвечает Продавец, последний обязуется полностью возвратить Покупателю полученные платежи не позднее пяти банковских дней от назначенной даты мероприятия.")
                .Replace("4. Разрешение споров","3.4. В случае отказа Покупателя от мероприятия последний должен компенсировать Продавцу понесённые им убытки на момент отказа.\n4. Разрешение споров")
                .Replace("4.1. Разногласия разрешаются путём переговоров. При недостижении соглашения спор передаётся на рассмотрение суда в установленном порядке.","4.1. Споры и разногласия по вопросам выполнения обязательств по договору разрешаются путём переговоров на основании действующего законодательства РФ.\n4.2. Если Стороны не придут к соглашению, спор подлежит разрешению в Арбитражном суде г. Москвы.")
                .Replace("5.2. Договор действует с момента подписания до исполнения обязательств. Договор составлен в двух экземплярах, по одному для каждой стороны.","5.2. Договор вступает в силу с момента подписания обеими Сторонами и действует до выполнения обязательств по договору.\n5.3. Об изменении реквизитов, а также других изменениях, касающихся договора, Стороны обязаны своевременно извещать друг друга в письменном виде.\n5.4. Договор составлен в двух экземплярах, имеющих одинаковую юридическую силу, по одному для каждой Стороны."),
            IssueDocumentKinds.Waybill=>template
                .Replace("ТОВАРНАЯ НАКЛАДНАЯ № {issueId}","Унифицированная форма № ТОРГ-12\nУтверждена постановлением Госкомстата России от 25.12.98 № 132\nФорма по ОКУД: 0330212. По ОКПО: ____________.\nТОВАРНАЯ НАКЛАДНАЯ № {issueId}")
                .Replace("Всего отпущено билетов: {count} ({countWords}).","Всего по накладной: {count} шт., на сумму {amount} руб.\nТоварная накладная имеет приложение на ____________ листах.\nВсего мест: {count} ({countWords}).\nМасса груза (нетто): ____________. Масса груза (брутто): ____________.\nПриложение (паспорта, сертификаты и т.п.) на ____________ листах.\nВсего отпущено на сумму: {amountWords}.")
                .Replace("Отпуск произвёл:","Главный (старший) бухгалтер: ____________ / ____________ /\nОтпуск произвёл:")
                .Replace("По доверенности № ____________ от ____________.","По доверенности № ____________ от ____________, выданной ____________.")+"\nМ.П. отправителя                          М.П. получателя",
            IssueDocumentKinds.Act=>template.Replace("НДС: ____________.","Без налога (НДС): ____________.\nВсего оказано услуг на сумму {amount} руб. ({amountWords}).")+"\nМ.П. исполнителя                          М.П. заказчика",
            IssueDocumentKinds.Bill=>template.Replace("НДС: ____________.","Всего к оплате: {amount} руб. ({amountWords}).\nНДС: ____________."),
            _=>template
        };
    }
    public static string PreviousDefault(string kind)=>kind switch
    {
        IssueDocumentKinds.Contract=>"""
            ДОГОВОР № {issueId}
            Дата заключения: {date}
            {sender}, именуемый в дальнейшем «Продавец» (представитель: {senderPosition} {senderSignatory}; основание полномочий: ____________), и {recipient}, именуемый в дальнейшем «Покупатель» (представитель: {recipientPosition} {recipientSignatory}; основание полномочий: ____________), заключили настоящий договор.
            1. Предмет договора
            1.1. Продавец обязуется передать Покупателю театральные билеты на мероприятие «{event}» на площадке {venue}. Дата и время мероприятия: {eventDate}. Количество: {count} шт. Стоимость: {amount} руб.
            1.2. Состав передаваемых билетов:
            {items}
            2. Порядок и условия платежа
            2.1. Общая стоимость договора: {amount} руб. ({amountWords}). НДС: ____________.
            2.2. Покупатель перечисляет оплату на расчётный счёт Продавца. Размер предоплаты: ____________. Срок оплаты: ____________.
            2.3. Срок и порядок передачи билетов: ____________.
            3. Ответственность сторон
            3.1. Стороны несут ответственность в соответствии с законодательством Российской Федерации.
            3.2. При отмене мероприятия по причинам, за которые отвечает Продавец, порядок и срок возврата платежей: ____________.
            3.3. Стороны освобождаются от ответственности за неисполнение обязательств вследствие обстоятельств непреодолимой силы.
            4. Разрешение споров
            4.1. Разногласия разрешаются путём переговоров. При недостижении соглашения спор передаётся на рассмотрение суда в установленном порядке.
            5. Заключительные положения
            5.1. Изменения оформляются письменно и подписываются обеими сторонами.
            5.2. Договор действует с момента подписания до исполнения обязательств. Договор составлен в двух экземплярах, по одному для каждой стороны.
            {summary}
            {total}
            6. Реквизиты и подписи сторон
            Продавец
            {senderDetails}
            Покупатель
            {recipientDetails}
            Продавец: {senderPosition} ____________ / {senderSignatory} /
            Покупатель: {recipientPosition} ____________ / {recipientSignatory} /
            """,
        IssueDocumentKinds.Act=>"""
            АКТ № {issueId}
            Дата составления: {date}
            Исполнитель: {sender}
            Заказчик: {recipient}
            Основание: договор № ____________ от ____________.
            Мероприятие: {event}
            Площадка: {venue}. Дата и время: {eventDate}.
            {items}
            {summary}
            {total}
            НДС: ____________.
            Вышеперечисленные услуги выполнены полностью и в срок. Заказчик претензий по объёму, качеству и срокам оказания услуг не имеет.
            Реквизиты исполнителя
            {senderDetails}
            Реквизиты заказчика
            {recipientDetails}
            Исполнитель: {senderPosition} ____________ / {senderSignatory} /
            Заказчик: {recipientPosition} ____________ / {recipientSignatory} /
            """,
        IssueDocumentKinds.Waybill=>"""
            ТОВАРНАЯ НАКЛАДНАЯ № {issueId}
            на передачу театральных билетов
            Дата составления: {date}
            Грузоотправитель и поставщик
            {senderDetails}
            Грузополучатель и плательщик
            {recipientDetails}
            Основание: договор № ____________ от ____________.
            Мероприятие: {event}
            Площадка: {venue}. Дата и время: {eventDate}.
            {items}
            {summary}
            {total}
            НДС: ____________.
            Всего отпущено билетов: {count} ({countWords}).
            Отпуск разрешил: {senderPosition} ____________ / {senderSignatory} /
            Отпуск произвёл: ____________ / ____________ /
            По доверенности № ____________ от ____________.
            Груз принял: ____________ / ____________ /
            Груз получил грузополучатель: {recipientPosition} ____________ / {recipientSignatory} /
            """,
        IssueDocumentKinds.Bill=>"""
            СЧЁТ НА ОПЛАТУ № {issueId}
            Дата составления: {date}
            Поставщик и получатель платежа
            {senderDetails}
            Покупатель и плательщик
            {recipientDetails}
            Основание: договор № ____________ от ____________.
            Мероприятие: {event}
            Площадка: {venue}. Дата и время: {eventDate}.
            {items}
            {summary}
            {total}
            НДС: ____________.
            Срок оплаты: ____________.
            Руководитель: {senderPosition} ____________ / {senderSignatory} /
            Главный бухгалтер: ____________ / ____________ /
            """,
        _=>throw new TicketException("Неизвестный вид документа.")
    };
    public static int[] ItemWidths(string kind)=>kind switch
    {
        IssueDocumentKinds.Waybill=>[400,3800,600,500,650,650,700,700,750,700,1000,1000,650,750,1200],
        IssueDocumentKinds.Bill=>[350,3500,500,650,850,1000,750,1100,650],
        _=>[450,4450,600,850,1150,1300]
    };
    public static string Items(InvoiceInfo invoice,string kind="contract")
    {
        var rows=new List<string>{kind switch
        {
            IssueDocumentKinds.Waybill=>"№ | Билеты и места | Код | Ед. | ОКЕИ | Упак. | В месте | Мест, шт. | Брутто | Кол-во | Цена, ₽ | Без НДС, ₽ | НДС, % | НДС, ₽ | С НДС, ₽",
            IssueDocumentKinds.Bill=>"№ | Билеты и места | Ед. | Кол-во | Цена, ₽ | Сумма, ₽ | НДС | С НДС, ₽ | Валюта",
            _=>"№ | Билеты и места | Ед. | Кол-во | Цена, ₽ | Сумма, ₽"
        }};
        var index=0;
        foreach(var line in InvoiceDocuments.Lines(invoice))
        {
            var description=$"Театральные билеты: {Cell(invoice.Title)}. {Cell(invoice.Venue)}. {Cell(invoice.EventDate)}. {Cell(line.Sector)}, ряд {Cell(line.Row)}, места {Cell(line.Seats)}";
            var total=DocumentTotals.Amount(line.Total);var price=DocumentTotals.Amount(line.Price);
            rows.Add(kind switch
            {
                IssueDocumentKinds.Waybill=>$"{++index} | {description} | — | шт. | 796 | — | — | {line.Count} | — | {line.Count} | {price} | {total} | — | — | {total}",
                IssueDocumentKinds.Bill=>$"{++index} | {description} | шт. | {line.Count} | {price} | {total} | — | {total} | RUB",
                _=>$"{++index} | {description} | шт. | {line.Count} | {price} | {total}"
            });
        }
        return string.Join("\n",rows);
    }
    private static string Cell(string value)=>value.Replace('|','/').Replace('\r',' ').Replace('\n',' ');
    public static string Requisites(OrganizationDetails details)
    {
        var lines=new List<string>();
        void Add(string label,string value){if(!string.IsNullOrWhiteSpace(value))lines.Add(label+value.Trim());}
        Add("",details.Name);Add("ОГРН / ОГРНИП: ",details.Ogrn);Add("ИНН: ",details.Inn);Add("КПП: ",details.Kpp);
        Add("Адрес: ",details.Address);Add("ОКПО: ",details.Okpo);Add("ОКВЭД: ",details.Okved);Add("ОКАТО: ",details.Okato);
        Add("Банк: ",details.Bank);Add("БИК: ",details.Bik);Add("Расчётный счёт: ",details.Account);Add("Корр. счёт: ",details.CorrespondentAccount);Add("Телефон: ",details.Phone);
        foreach(var field in details.ExtraFields??[])if(!string.IsNullOrWhiteSpace(field.Label))Add(field.Label.Trim()+": ",field.Value);
        return string.Join("\n",lines);
    }
    public static string Missing(string value)=>string.IsNullOrWhiteSpace(value)?"":value;
}
