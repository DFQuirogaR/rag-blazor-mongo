namespace BlazorApp.Data;

public static class ApplicationRoles
{
    public const string Administrator = "Rag.Administrator";
    public const string DocumentAuditor = "Rag.DocumentAuditor";
    public const string N1Agent = "Rag.N1Agent";
}

public static class ApplicationPolicies
{
    public const string DocumentsView = "Documents.View";
    public const string DocumentsProcess = "Documents.Process";
    public const string DocumentsDelete = "Documents.Delete";
    public const string ChatUse = "Chat.Use";
    public const string StatisticsView = "Statistics.View";
    public const string AdministrationManage = "Administration.Manage";
}
