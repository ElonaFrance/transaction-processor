namespace TransactionProcessor.Application.Domain.Constants;

public static class AppConstants
{
    public static class Kafka
    {
        public const string TransactionEventsTopic = "transaction-events";
        public const string ConsumerGroupId = "transaction-processor-group";
    }

    public static class StoredProcedures
    {
        public const string InsertTransaction = "usp_InsertTransaction";
        public const string UpsertAggregation = "usp_UpsertAggregation";
    }

    public static class MockData
    {
        public static readonly string[] ClientIds =
        [
            "CLIENT-001", "CLIENT-002", "CLIENT-003", "CLIENT-004", "CLIENT-005",
            "CLIENT-006", "CLIENT-007", "CLIENT-008", "CLIENT-009", "CLIENT-010"
        ];

        public static readonly string[] BankMerchants =
        [
            "SALARY PAYMENT", "ATM WITHDRAWAL SANDTON", "INTERNET TRANSFER",
            "DEBIT ORDER VODACOM", "DEBIT ORDER NETFLIX", "ATM WITHDRAWAL CBD",
            "SALARY CAPITEC", "EFT PAYMENT", "STOP ORDER MEDICAL AID"
        ];

        public static readonly string[] CreditCardMerchants =
        [
            "WOOLWORTHS FOOD", "CHECKERS HYPER", "PICK N PAY", "SHELL GARAGE",
            "ENGEN PETROLEUM", "SPUR RESTAURANTS", "STEERS", "NANDOS",
            "CLICKS PHARMACY", "DISCHEM", "MAKRO ONLINE", "TAKEALOT",
            "NETFLIX SUBSCRIPTION", "SPOTIFY", "AMAZON PRIME", "UBER EATS",
            "VIDA E CAFFE", "WOOLWORTHS CLOTHING", "H&M FASHION", "ZARA"
        ];

        public static readonly string[] InvestmentMerchants =
        [
            "SYGNIA ETF BUY", "CORONATION UNIT TRUST", "EASY EQUITIES BUY",
            "SATRIX TOP40 PURCHASE", "DIVIDEND INCOME SYGNIA", "EASY EQUITIES SELL",
            "ALLAN GRAY WITHDRAWAL", "MOMENTUM INVEST"
        ];
    }
}
