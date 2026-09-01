using System.ComponentModel.DataAnnotations;

namespace TravelBlog.Web.Models;

public enum PostCategory
{
    [Display(Name = "Literature and Stuff")]
    LiteratureAndStuff = 1,

    [Display(Name = "Humor and Satire")]
    FictionAndSatire = 2,

    [Display(Name = "Other")]
    Other = 3,

    [Display(Name = "Contact")]
    Contact = 4
}