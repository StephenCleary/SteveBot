using CsvHelper;
using System.Globalization;
using System.Xml;

const string userId = "263693";
const string folder = @"../../../../input";

List<PostsOfInterest> postsOfInterest = null!;
if (!File.Exists(Path.Combine(folder, "posts-of-interest.csv")))
{
	List<(string PostId, string PostParentId, string PostAsOfTime)> answers = [];

	// Find answers written by me.
	var reader = XmlReader.Create(Path.Combine(folder, "Posts.xml"));
	while (reader.Read())
	{
		if (reader.NodeType == XmlNodeType.Element && reader.Name == "row")
		{
			var postTypeId = reader.GetAttribute("PostTypeId") ?? throw new Exception();
			if (postTypeId != "2")
				continue;

			var postUserId = reader.GetAttribute("OwnerUserId");
			if (postUserId != userId)
				continue;

			var postId = reader.GetAttribute("Id") ?? throw new Exception();
			var postParentId = reader.GetAttribute("ParentId") ?? throw new Exception();

			var postLastEditorUserId = reader.GetAttribute("LastEditorUserId");
			var postAsOfTime = (postLastEditorUserId == userId) ?
				(reader.GetAttribute("LastEditDate") ?? throw new Exception()) :
				(reader.GetAttribute("CreationDate") ?? throw new Exception());

			answers.Add((postId, postParentId, postAsOfTime));
			Console.WriteLine($"Found answer {postId} for question {postParentId} at {postAsOfTime}.");
		}
	}

	Console.WriteLine($"Retrieved {answers.Count} answers.");

	var questionTitles = new Dictionary<string, string>(answers.Count);
	foreach (var (_, answerPostParentId, _) in answers)
		questionTitles[answerPostParentId] = null!;

	// Find question titles for my answers.
	reader = XmlReader.Create(Path.Combine(folder, "Posts.xml"));
	while (reader.Read())
	{
		if (reader.NodeType == XmlNodeType.Element && reader.Name == "row")
		{
			var postTypeId = reader.GetAttribute("PostTypeId") ?? throw new Exception();
			if (postTypeId != "1")
				continue;

			var postId = reader.GetAttribute("Id") ?? throw new Exception();
			if (!questionTitles.ContainsKey(postId))
				continue;

			questionTitles[postId] = reader.GetAttribute("Title") ?? throw new Exception();
			Console.WriteLine($"Found question title {questionTitles[postId]} for question {postId}.");
		}
	}

	postsOfInterest = new List<PostsOfInterest>(answers.Count);
	foreach (var (answerPostId, questionPostId, postAsOfTime) in answers)
	{
		if (!questionTitles.TryGetValue(questionPostId, out var questionTitle))
			throw new InvalidOperationException($"No title found for question {questionPostId}.");
		postsOfInterest.Add(new PostsOfInterest
		{
			QuestionPostId = questionPostId,
			QuestionTitle = questionTitle,
			AnswerPostId = answerPostId,
			PostAsOfTime = postAsOfTime,
		});
	}

	Console.WriteLine($"Retrieved {questionTitles.Count} questions.");

	using (var writer = new StreamWriter(Path.Combine(folder, "posts-of-interest.csv")))
	using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
		csv.WriteRecords(postsOfInterest);
}
else
{
	using (var reader = new StreamReader(Path.Combine(folder, "posts-of-interest.csv")))
	using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
		postsOfInterest = csv.GetRecords<PostsOfInterest>().ToList();
}

// Find post bodies for my answers and their questions.
var postBodies = new Dictionary<string, Dictionary<string, string>>(postsOfInterest.Count * 2);
foreach (var post in postsOfInterest)
{
	if (!postBodies.ContainsKey(post.QuestionPostId))
		postBodies[post.QuestionPostId] = [];

	if (!postBodies.ContainsKey(post.AnswerPostId))
		postBodies[post.AnswerPostId] = [];
}

{
	var reader = XmlReader.Create(Path.Combine(folder, "PostHistory.xml"));
	while (reader.Read())
	{
		if (reader.NodeType == XmlNodeType.Element && reader.Name == "row")
		{
			var postId = reader.GetAttribute("PostId") ?? throw new Exception();
			if (!postBodies.TryGetValue(postId, out var postDict))
				continue;

			var postHistoryTypeId = reader.GetAttribute("PostHistoryTypeId") ?? throw new Exception();
			if (postHistoryTypeId != "2" && postHistoryTypeId != "5" && postHistoryTypeId != "8")
				continue;

			var postHistoryCreationDate = reader.GetAttribute("CreationDate") ?? throw new Exception();
			var postText = reader.GetAttribute("Text") ?? throw new Exception();

			if (postDict.ContainsKey(postHistoryCreationDate))
				Console.WriteLine($"Post {postId} has multiple bodies for {postHistoryCreationDate} - last in wins!");

			postDict[postHistoryCreationDate] = postText;
			Console.WriteLine($"Found post body for {postId} at {postHistoryCreationDate}");
		}
	}
}

List<StackOverflowRecords> results = new(postsOfInterest.Count);
foreach (var post in postsOfInterest)
{
	string FindBody(string postId)
	{
		var dict = postBodies[postId];
		if (dict.Count == 0)
			throw new InvalidOperationException($"Post {postId} does not have a body as of {post.PostAsOfTime}");
		var postTimesBeforePostAsOfTime = dict.Keys.Where(x => x.CompareTo(post.PostAsOfTime) <= 0).ToList();
		if (postTimesBeforePostAsOfTime.Count == 0)
		{
			var earliest = dict.Keys.Min()!;
			Console.WriteLine($"Post {postId} does not have a body as of {post.PostAsOfTime}; taking body from {earliest}");
			return dict[earliest];
		}

		return dict[postTimesBeforePostAsOfTime.Max()!];
	}

	results.Add(new()
	{
		AnswerPostId = post.AnswerPostId,
		QuestionPostId = post.QuestionPostId,
		QuestionTitle = post.QuestionTitle,
		AnswerBody = FindBody(post.AnswerPostId),
		QuestionBody = FindBody(post.QuestionPostId),
	});
}

using (var writer = new StreamWriter(Path.Combine(folder, "stack-overflow-posts.csv")))
using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
	csv.WriteRecords(results);

Console.WriteLine("Done!");

sealed class PostsOfInterest
{
	public string QuestionPostId { get; set; } = null!;
	public string QuestionTitle { get; set; } = null!;
	public string AnswerPostId { get; set; } = null!;
	public string PostAsOfTime { get; set; } = null!;
}

sealed class StackOverflowRecords
{
	public string QuestionPostId { get; set; } = null!;
	public string QuestionTitle { get; set; } = null!;
	public string AnswerPostId { get; set; } = null!;
	public string QuestionBody { get; set; } = null!;
	public string AnswerBody { get; set; } = null!;
}