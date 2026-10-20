namespace Tickets.Core;

/// <summary>Editable business forms based on the supplied contract, act, bill and goods waybill.</summary>
public static class BusinessDocumentTemplates
{
    public static string LegacyDefault(string kind)=>IssueDocumentKinds.Name(kind)+" № {issueId}\nДата: {date}\nОтправитель: {sender}\nПолучатель: {recipient}\nМероприятие: {event}\nДата мероприятия: {eventDate}\nПлощадка: {venue}\n\nПередача билетов по указанной выдаче.\n\n{summary}\n{total}\n\nПередал: ____________________    Получил: ____________________";
    public static string Default(string kind)=>kind switch
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
    public static string Items(InvoiceInfo invoice)
    {
        var rows=new List<string>{"№ | Билеты и места | Ед. | Кол-во | Цена, ₽ | Сумма, ₽"};
        var index=0;
        foreach(var line in InvoiceDocuments.Lines(invoice))
            rows.Add($"{++index} | {Cell(line.Sector)}, ряд {Cell(line.Row)}, места {Cell(line.Seats)} | шт. | {line.Count} | {DocumentTotals.Amount(line.Price)} | {DocumentTotals.Amount(line.Total)}");
        return string.Join("\n",rows);
    }
    private static string Cell(string value)=>value.Replace('|','/').Replace('\r',' ').Replace('\n',' ');
    public static string Requisites(OrganizationDetails details)=>string.Join("\n",new[]{details.Name,
        "ИНН: "+Missing(details.Inn)+"; КПП: "+Missing(details.Kpp)+"; ОГРН / ОГРНИП: "+Missing(details.Ogrn),
        "Адрес: "+Missing(details.Address),"Банк: "+Missing(details.Bank)+"; БИК: "+Missing(details.Bik),
        "Расчётный счёт: "+Missing(details.Account),"Корр. счёт: "+Missing(details.CorrespondentAccount),
        "Подписант: "+Missing(details.Signatory)+"; должность: "+Missing(details.Position),"Телефон: "+Missing(details.Phone)});
    public static string Missing(string value)=>string.IsNullOrWhiteSpace(value)?"[не заполнено]":value;
}
