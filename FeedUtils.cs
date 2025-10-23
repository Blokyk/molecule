using System.ServiceModel.Syndication;
using System.Xml;

public static class FeedUtils
{
    public static SyndicationFeed Aggregate(SyndicationFeed[] feeds) {
        var finalFeed = new SyndicationFeed();

        foreach (var feed in feeds)
            finalFeed.AddFeed(feed);

        var description
            = "An aggregated feed made up of the following feeds:"
            + String.Join("", feeds.Select(f => $"\n  - {f.Title.Text} (from '{GetMainLink(f)}')"));

        finalFeed.Description = SyndicationContent.CreatePlaintextContent(description);

        finalFeed.ImageUrl = null;

        return finalFeed;
    }

    public static void AddFeed(this SyndicationFeed dest, SyndicationFeed src) {
        dest.AttributeExtensions.AddRange(src.AttributeExtensions);
        dest.ElementExtensions.AddRange(src.ElementExtensions);

        dest.Authors.AddRange(src.Authors);

        // add each categories from the source, but prefix them
        dest.Categories.AddRange(
            src.Categories.Select(cat => {
                var newCat = cat.Clone();
                if (cat.Name is not null or "") {
                    newCat.Name = $"{src.Title.Text}/{cat.Name}";
                }
                if (cat.Label is not null or "") {
                    newCat.Label = $"{src.Title.Text} — {cat.Label}";
                }
                return newCat;
            })
        );

        dest.Contributors.AddRange(src.Contributors);

        dest.LastUpdatedTime =
            dest.LastUpdatedTime < src.LastUpdatedTime
            ? src.LastUpdatedTime
            : dest.LastUpdatedTime;

        dest.Items = dest.Items.Concat(src.Items);
        dest.Links.AddRange(src.Links);
    }

    public static async Task<SyndicationFeed> GetFeedAsync(Uri uri, IHttpClientFactory httpClientFactory) {
        using var client = httpClientFactory.CreateClient();
        using var response = await client.GetStreamAsync(uri);
        using var feedReader = XmlReader.Create(response);
        return SyndicationFeed.Load(feedReader);
    }

    public static Uri? GetMainLink(SyndicationFeed feed)
        => feed.BaseUri ?? feed.Links.FirstOrDefault()?.Uri;
}