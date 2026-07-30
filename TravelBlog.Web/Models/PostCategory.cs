using System.ComponentModel.DataAnnotations;

namespace TravelBlog.Web.Models;

public enum PostCategory
{
    [Display(Name = "Parody Editorial")]
    ParodyEditorial = 1,

    [Display(Name = "Short Stories")]
    ShortStory = 2,

    [Display(Name = "Real News")]
    RealNews = 3
}