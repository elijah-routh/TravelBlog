namespace TravelBlog.Web.Models
{
    public class HomeIndexViewModel
    {
        public List<EditorialCategoryViewModel> Categories { get; set; } = [];
    }

    public class EditorialCategoryViewModel
    {
        public string Name { get; set; } = string.Empty;

        public string Slug { get; set; } = string.Empty;

        public List<Post> Posts { get; set; } = [];
    }

    public class EditorialPostViewModel
    {
        public string Title { get; set; } = string.Empty;

        public string Summary { get; set; } = string.Empty;

        public string ImageUrl { get; set; } = string.Empty;

        public string PostUrl { get; set; } = "#";
    }
}