using System.ComponentModel.DataAnnotations;

namespace TravelBlog.Web.Models;

public enum PostCategory
{
    [Display(Name = "Literature and Stuff")]
    LiteratureAndStuff = 1,

    [Display(Name = "Fiction and Satire")]
    FictionAndSatire = 2,

    [Display(Name = "Other")]
    Other = 3
}