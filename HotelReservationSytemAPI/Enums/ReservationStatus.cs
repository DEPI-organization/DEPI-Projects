using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
public enum ReservationStatus
{
    [Display(Name = "Confirmed")]
    Confirmed = 1,

    [Display(Name = "Cancelled")]
    Cancelled = 2,

    [Display(Name = "Completed")]
    Completed = 3
}