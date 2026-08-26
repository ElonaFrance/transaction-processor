using TransactionProcessor.Application.Domain.Events;
using TransactionProcessor.Application.Services.Contracts;

namespace TransactionProcessor.Application.Services;

public class CategoryService : ICategoryService
{
    private static readonly (string[] Keywords, int CategoryId, string CategoryName)[] Rules =
    [
        (["woolworths food", "checkers", "pick n pay", "spar", "shoprite", "food lovers", "grocery"], 1, "Groceries"),
        (["shell", "engen", "bp ", "caltex", "total petroleum", "garage", "petrol", "fuel"], 2, "Fuel"),
        (["eskom", "city power", "telkom", "vodacom", "mtn ", "cell c", "rain ", "water", "rates", "municipal", "debit order"], 3, "Utilities"),
        (["netflix", "showmax", "dstv", "spotify", "amazon prime", "apple tv", "disney+", "cinema", "ticket"], 4, "Entertainment"),
        (["restaurant", "spur", "steers", "nandos", "kfc ", "mcdonalds", "ocean basket", "vida", "caffe", "coffee", "uber eats", "mr delivery", "food24"], 5, "Dining"),
        (["clicks", "dischem", "pharmacy", "hospital", "mediclinic", "netcare", "doctor", "medical aid", "discovery health"], 6, "Healthcare"),
        (["uber ", "bolt ", "airport", "kulula", "flysafair", "british airways", "hotel", "airbnb", "booking.com"], 7, "Travel"),
        (["takealot", "amazon", "makro", "game store", "hi-fi corp", "h&m", "zara", "woolworths clothing", "truworths", "mr price"], 8, "Shopping"),
        (["salary", "payroll", "wages", "remuneration"], 9, "Salary"),
        (["transfer", "eft ", "internet banking", "payment to", "payment from", "stop order"], 10, "Transfer"),
        (["atm withdrawal", "atm cash"], 11, "ATM"),
        (["insurance", "old mutual", "sanlam", "liberty", "momentum insure", "outsurance"], 12, "Insurance"),
        (["school fee", "university", "college", "education", "tuition", "unisa"], 13, "Education"),
        (["subscription", "monthly fee", "annual fee", "membership"], 14, "Subscriptions"),
    ];

    public (int CategoryId, string CategoryName) Categorise(TransactionEvent transactionEvent)
    {
        var searchText = $"{transactionEvent.Description} {transactionEvent.MerchantName}".ToLowerInvariant();

        foreach (var (keywords, categoryId, categoryName) in Rules)
        {
            if (keywords.Any(k => searchText.Contains(k)))
                return (categoryId, categoryName);
        }

        return (99, "Other");
    }
}
