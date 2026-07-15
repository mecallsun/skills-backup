namespace DormManage.Shared.Models;
public class BillingStandard
{
    public int Id { get; set; }
    public string StandardName { get; set; } = string.Empty;
    public string? ApplicableType { get; set; }
    public decimal HotWaterUnitPrice { get; set; }
    public decimal ColdWaterUnitPrice { get; set; }
    public decimal ElectricUnitPrice { get; set; }
    public DateOnly? EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
}
