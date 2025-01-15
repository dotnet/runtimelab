// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Dynamo;

namespace Dynamo.CSLang;

/// <summary>
/// Represents a single line comment in C#.
/// </summary>
public class CSComment : SimpleLineElement
{
    /// <summary>
    /// Constructs a new comment with the specified text. Note that the text must not contain newlines.
    /// </summary>
    /// <param name="text">the text of the comment</param>
    public CSComment(string text)
        : base(Commentize(text), indent: false, prependIndents: true, allowSplit: false)
    {
        Text = text;
    }

    /// <summary>
    /// Prepends the comment token to the text.
    /// </summary>
    /// <param name="text">the text to be turned into a comment. Note that the text must not contain newlines.</param>
    /// <returns>the commentized text</returns>
    /// <exception cref="ArgumentException">throws if the text contains a newline</exception>
    static string Commentize(string text)
    {
        if (text.Contains("\n"))
            throw new ArgumentException("Comment text must not contain newlines.", nameof(text));
        return "// " + text;
    }

    /// <summary>
    /// The text of the comment. This does NOT include the comment token.
    /// </summary>
    public string Text { get; private set; }
}

/// <summary>
/// Represents a block of comments in C#.
/// </summary>
public class CSCommentBlock : CodeElementCollection<CSComment>
{
    /// <summary>
    /// Constructs a new empty comment block.
    /// </summary>
    public CSCommentBlock()
        : base()
    {
    }

    /// <summary>
    /// Constructs a new comment block with the specified comments.
    /// </summary>
    /// <param name="comments">the comments to add</param>
    public CSCommentBlock(params CSComment[] comments)
        : base()
    {
        AddRange(comments);
    }

    /// <summary>
    /// Constructs a new comment block with the specified comment text.
    /// </summary>
    /// <param name="comments">the text of the comments to add</param>
    public CSCommentBlock(params string[] comments)
        : base()
    {
        AddRange(comments.Select(c => new CSComment(c)));
    }

    /// <summary>
    /// Sanitize the text into comments. If the text contains newlines, they will be split into separate comments.
    /// </summary>
    /// <param name="text">The text to convert into comments</param>
    /// <returns>An enumeration of the resulting comments</returns>
    static IEnumerable<CSComment> Sanitize(string[] text)
    {
        foreach (var s in text)
        {
            var lines = s.Split('\n');
            foreach (var line in lines)
            {
                yield return new CSComment(line);
            }
        }
    }

    /// <summary>
    /// A fluent interface to add a comment. This can be used like this: comments.And("comment0").And("comment1")...
    /// </summary>
    /// <param name="text">The text to add. If the text contains newlines, it will be split into multiple comments</param>
    /// <returns>the comment block with the added comment(s)</returns>
    public CSCommentBlock And(string text)
    {
        AddRange(Sanitize(new string[] { text }));
        return this;
    }

    /// <summary>
    /// A fluent interface to add a comment. This can be used like this: comments.And(comment0).And(comment1)...
    /// </summary>
    /// <param name="comment">The comment to add</param>
    public CSCommentBlock And(CSComment comment)
    {
        Add(comment);
        return this;
    }
}
