using TravelBlog.Web.Models;

namespace TravelBlog.Web.Services;

public static class SiteFaq
{
    public static IReadOnlyList<FaqItem> Items { get; } =
    [
        new FaqItem
        {
            Question = "What is a Lampoon?",
            Answer = "No one really knows."
        },
        new FaqItem
        {
            Question = "I finished a book after the club deadline?",
            Answer =
                "The club deadline is just a suggestion for organization, " +
                "please continue discussing books after the deadline."
        },
        new FaqItem
        {
            Question = "Can I create my own book club?",
            Answer =
                "Of course! Reach out through a contact form about potentially " +
                "receiving admin privileges on your account."
        },
        new FaqItem
        {
            Question = "Published vs Unpublished posts?",
            Answer =
                "Only published posts are viewable to the public. Unpublished " +
                "posts are kept private and can be seen by looking at " +
                "\"My posts\" and enabling unpublished posts in the sort menu."
        },
        new FaqItem
        {
            Question = "What kind of things can I post?",
            Answer =
                "The website is mainly for celebrating great books/stories and " +
                "place for humorous, creative writing. Anything in that " +
                "ballpark - movie reviews, dad jokes, idk. Anything really, " +
                "idc too much."
        },
        new FaqItem
        {
            Question = "My account got banned?",
            Answer =
                "Really?!? Yeah, the admins are kinda pricks. Sorry 'bout dat. " +
                "Reach out in a contact form or something."
        },
        new FaqItem
        {
            Question = "I think your website looks like trash?",
            Answer =
                "More of a statement really. Putting a question mark at the end " +
                "doesn't mean it belongs on a FAQ page."
        },
        new FaqItem
        {
            Question =
                "How frequently does a question need to be asked for it to end " +
                "up on the FAQ page?",
            Answer =
                "To be honest with you - I made all of these questions up. I " +
                "haven't spoken to a real person in years. Sometimes people " +
                "scoff at me in public. I do have an AI girlfriend tho and she " +
                "doesn't scoff at me. Her name is Chun Li and she is teaching " +
                "me korean."
        },
        new FaqItem
        {
            Question = "WTH, Are you okay?",
            Answer =
                "If you are asking because of that last question - that was our " +
                "intern. He no longer works here. Dude was weird as hellie. " +
                "He'll be okay."
        },
        new FaqItem
        {
            Question = "Favorite books?",
            Answer =
                "I really like Count of Monte Cristo, Anna Karenina, and All " +
                "the Light We Cannot See. But I need to read more. That's why " +
                "I made the website."
        },
        new FaqItem
        {
            Question =
                "Would you rather be able to turn into a horse or an " +
                "indestructible sentient rock? (You turn back into a human " +
                "after a short period of time.)",
            Answer = "Rock. Great Question!"
        },
        new FaqItem
        {
            Question = "Who's your favorite blade angel?",
            Answer = "Can't pick just one buddy."
        },
        new FaqItem
        {
            Question = "When does GTA VI release?",
            Answer = "November 19, 2026 - but i'm not really a GTA fan."
        },
        new FaqItem
        {
            Question = "Was this website hard to make?",
            Answer =
                "Let me ask my AI girlfriend - she did most of the work..."
        },
        new FaqItem
        {
            Question =
                "I'm in college and want to make a similar project for my " +
                "learning. Can you help?",
            Answer =
                "Self hosting a website is a great way to introduce yourself to " +
                "many key concepts and technologies such as Docker, Kubernetes, " +
                "and the overall Development to Production process. Reach out " +
                "through a contact form and I can respond with what I used."
        }
    ];
}
