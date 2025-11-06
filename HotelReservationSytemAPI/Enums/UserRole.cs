using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
public enum UserRole
{
    [Display(Name = "User")]
    User = 1,

    [Display(Name = "Administrator")]
    Admin = 2
}