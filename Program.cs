using System.Xml;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddOpenApi();
builder.Services.AddHttpClient();

builder.WebHost.ConfigureKestrel((opt) => {
    // System.ServiceModel.Syndication doesn't support async :(
    opt.AllowSynchronousIO = true;
});

var moleculesStr = builder.Configuration["MoleculesPath"];
if (!Uri.TryCreate(moleculesStr, UriKind.Absolute, out var moleculesPath)) {
    throw new ArgumentException("Not a valid URI", "MoleculesPath");
}

if (!moleculesPath.IsFile) {
    throw new ArgumentException("Path should point to a local folder", "MoleculesPath");
}

if (!Utils.TryGetDirectory(moleculesPath.AbsolutePath, out var moleculesFolder) || !moleculesFolder.Exists) {
    throw new ArgumentException("Couldn't find or access the specified feed folder", "MoleculesPath");
}

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment()) {
    app.MapOpenApi();
}

// get a list of all links in each file (molecule)
var molecules = new Dictionary<string, Uri[]>();
foreach (var file in moleculesFolder.EnumerateFiles()) {
    var moleculeName = file.Name[..^file.Extension.Length];
    var links =
        File.ReadAllLines(file.FullName)
            .Select(linkStr => new Uri(linkStr))
            .ToArray();

    molecules.Add(moleculeName, links);
}

app.Logger.LogInformation(
    "Found {feedCount} molecules: {feedList}",
    molecules.Count,
    String.Join(", ", molecules.Keys));

var httpClientFactory = app.Services.GetService<IHttpClientFactory>()!;

// create an endpoint for each molecule
foreach (var kv in molecules) {
    // note: you might be thinking:
    //           > zoë, are you dumb? are you big dumb? why not just
    //           > deconstruct it directly in the foreach declaration?
    //       and yeah i agree, this *is* very dumb. you might think that this
    //       code is exactly equivalent to deconstructing in the loop header
    //       directly, but i guess not??? this one is correctly captured inside
    //       the lambda, whereas putting the deconstruction in the header
    //       messes up the closure somehow. something about modified lambda
    //       captures or w/ever [^1]. honestly even looking at the lowered
    //       code, i can't see why this works but not header deconstruction x_x
    //
    //       [^1]: https://pvs-studio.com/en/blog/posts/csharp/0468/
    var (moleculeName, atomLinks) = kv;
    app.MapGet($"/{moleculeName}.xml", async context => {
        app.Logger.LogInformation("Generating feed for {name}", moleculeName);
        var atoms = await Task.WhenAll(
            atomLinks.Select(
                async link => await FeedUtils.GetFeedAsync(link, httpClientFactory)
            )
        );

        var molecule = FeedUtils.Aggregate(atoms);

        molecule.Generator = "blokyk/molecule";
        molecule.Id = "molecule:" + moleculeName;

        context.Response.ContentType = "application/atom+xml";
        using var bodyXmlWriter = XmlWriter.Create(context.Response.Body, new XmlWriterSettings() { Indent = true });
        molecule.SaveAsAtom10(bodyXmlWriter);
    });
}

app.Run();