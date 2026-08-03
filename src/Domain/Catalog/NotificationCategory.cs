namespace Kart.Notification.Domain.Catalog;

/// <summary>
/// ddd-model.md Modeling Decision #4: an engineering-default category taxonomy (no BRD-enumerated
/// list exists) that <c>notification_preferences.opt_out_matrix</c> is keyed by, per category.
/// </summary>
public static class NotificationCategory
{
    public const string OrderUpdates = "order-updates";
    public const string Payment = "payment";
    public const string Shipping = "shipping";
    public const string Marketing = "marketing";
    public const string Account = "account";
}
