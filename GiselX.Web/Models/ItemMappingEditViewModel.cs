using System.ComponentModel.DataAnnotations;

namespace GiselX.Web.Models;

public sealed class ItemMappingEditViewModel
{
    public int CompanyId { get; set; }
    public int? Id { get; set; }

    [Required, Display(Name = "GMK Item ID")]
    public string GmkItemId { get; set; } = string.Empty;

    [Required, Display(Name = "GMK Item Name")]
    public string GmkItemName { get; set; } = string.Empty;

    [Required, Display(Name = "Distributor Item ID")]
    public string DistributorItemId { get; set; } = string.Empty;

    [Required, Display(Name = "Distributor Item Name")]
    public string DistributorItemName { get; set; } = string.Empty;
}
