# System.CommandLine

The purpose of this library is to make command line tools first class by
providing a command line parser. We've already made an attempt in 2009, but
that wasn't a design we (or the community) was
[happy with](http://tirania.org/blog/archive/2009/Feb-21.html).

Here are the goals:

* Designed for cross-platform usage
* Lightweight with minimal configuration
* Optional but built-in support for help, validation, and response files
* Support for multiple commands, like version control tools

[Syntax](#syntax) and [API samples](#api-samples) are below.

## Why a new library?

There is already a set of libraries available for command line parsing, such as:

* [Mono.Options][Mono.Options] (also known as `NDesk.Options`)
* [CommandLine][CommandLine]
* An [internal one][vance] that Vance Morrison wrote many years ago.

[Mono.Options]: http://tirania.org/blog/archive/2008/Oct-14.html
[CommandLine]: https://github.com/gsscoder/commandline
[vance]: https://github.com/dotnet/buildtools/blob/master/src/common/CommandLine.cs

So the question is: why a new one? There are a couple of reasons:

1. We want to support a syntax that feels natural when used across platforms. In
   particular, we want to be very close to the Unix- and GNU style.

2. We need something that is quite low level. In particular, we don't want to
   have a library that requires reflection for attribute discovery or for
   setting properties.

3. We want an experience that achieves an extremely minimal setup in terms of
   lines of code required for parsing.

While some of the libraries solve some of these aspects, none of them solve the
combination.

Of course, providing a command line parser isn't just providing a parsing
mechanism: in order to be usable, the library has to be opinionated in both the
supported syntax, as well as in the shape of the APIs. In the BCL, we've always
taken the stance that we want to provide a layered experience that allows
getting the 80% scenario done, while allowing to be extensible for a potential
long tail of additional scenarios. If that means we've had to make policy decisions,
so be it because not making those forces all of our consumers to come up with
their own policy.

That being said, the goal isn't to provide the final command line parser
library. In fact, I'm not aware of any platform that gets away with having a
single one. If you're happy with the one you're already using or if you even
wrote your own: that's perfectly fine. But after one and a half decades it's
time for the BCL to provide a built-in experience as well.

## Work in progress

* Should we support a case insensitive mode?
* Should we have a way to name option arguments, e.g. `DefineOption("n=name")`?
* Should provide a string based approach to define usage?
* Should we provide an error handler?
* Should we provide a help request handler?
* Should we expose the response file hander?
* Should we allow "empty" commands, so that the tool can support options without
  a command, like `git --version`?

## Syntax

The syntax conventions are heavily inspired by the following existing
conventions:

* [Unix History][Unix-History]
* [POSIX Conventions][POSIX-Conventions]
* [GNU Standards for Command Line Interfaces][GNU]
* [GNU List of standard option names][GNU-Options]

[Unix-History]: http://catb.org/~esr/writings/taoup/html/ch10s05.html
[POSIX-Conventions]: http://www.cs.unicam.it/piergallini/home/materiale/gc/java/essential/attributes/_posix.html
[GNU]: http://www.gnu.org/prep/standards/html_node/Command_002dLine-Interfaces.html
[GNU-Options]: http://www.gnu.org/prep/standards/html_node/Option-Table.html#Option-Table

In general, all strings are treated in a case sensitive way. This allows
supporting options that only differ by case, which is pretty common on
Unix systems, e.g.

    # This reverses the output
    $ ls -r *.txt
    # This does a recursive search
    $ ls -R *.txt

### Single character options

Single character options are delimited by a single dash, e.g.

    $ tool -x -d -f

They can be *bundled* together, such as

    $ tool -xdf

Please note that slashes aren't supported.

### Keyword options

Keyword options are delimited by two dashes, such as:

    $ tool --verbose

### Option arguments

Both, the single letter form, as well as the long forms, support arguments.
Arguments must be separated by either a space, an equal sign or a colon:

    # All three forms are identical:
    $ tool --out result.exe
    $ tool --out=result.exe
    $ tool --out:result.exe

Multiple spaces are allowed as well:

    $ tool --out  result.exe
    $ tool --out =   result.exe
    $ tool --out : result.exe

This even works when combined with bundling, but in that case only the last
option can have an argument. So this:

    $ tool -am "hello"

is equivalent to:

    $ tool -a -m "hello"

### Multiple occurrences

Unix has a strong tradition for scripting. In order to make it easier to forward
arguments to scripts, it's common practice to allow options to appear more than
once. The semantics are that the last one wins. So this:

    $ tool -a this -b -a that

has the same effect as that:

    $ tool -b -a that

### Parameters

Parameters, sometimes also called non-option arguments, can be anywhere in the
input:

    # Both forms equivalent:
    $ tool input1.ext input2.ext -o result.ext
    $ tool input1.ext -o result.ext input2.ext

### Commands

Very often, tools have multiple commands with independent options. Good example
are version control tools, e.g.

    $ tool fetch origin --prune
    $ tool commit -m 'Message'

### Response files

It's common practice to allow passing command line arguments via response files.
This can look as follows:

    $ tool -f -r @D:\src\defaults.rsp --additional

The API supports multiple response files being passed in. It will simply expand
those in-place, i.e. it's valid to have additional options and parameters
before, as well as after the response file reference.

## API Samples

### Hello world

```C#
using System;
using System.CommandLine;

static class Program
{
    static void Main(string[] args)
    {
        var addressee = "world";

        ArgumentSyntax.Parse(args, syntax =>
        {
            syntax.DefineOption("n|name", ref addressee, "The addressee to greet");
        });

        Console.WriteLine("Hello {0}!", addressee);
    }
}
```

Usage looks as follows:

```
$ ./hello -h
usage: hello [-n <arg>]

    -n, --name    The addressee to greet

$ ./hello
Hello world!
$ ./hello -n Jennifer
Hello Jennifer!
$ ./hello --name Tom
Hello Tom!
$ ./hello -x
error: invalid option -x
```

### Defining options

The `ArgumentSyntax` class allows defining options and parameters for any data
type. In order to parse the value, you need to supply a `Func<string, T>` that
performs the parsing. So if you want to use a guid, you could define an option
like this:

```C#
Guid guid = Guid.Empty;
syntax.DefineOption("g|guid", ref guid, Guid.Parse, "The GUID to use");
```

The library provides overloads that handle the most common types, such as
`string`, `int`, and `bool`, so that you don't have to pass in parsers for
those.

Boolean options are specially handled in that they are considered flags, i.e.
they don't require an argument -- they are simply considered `true` if they are
specified. However you can still explicitly pass in `true` or `false`. So this

    $ tool -x

Is equivalent to

    $ tool -x:true
    
Normally, options must supply an argument value (or it's considered an error). However, there may be times when you want to allow a particular option whether it supplies a value or not. To do this, use the overloaded `DefineOption()` methods:

```C#
int port = 1234;
var option = syntax.DefineOption("s|server", ref port, false,
    "Start the server, optionally specifying a port");
```

Then the value in code will be set regardless of whether the command line option specifies one and an error will not be thrown if the option value is omitted. You can check for whether the option was specified at all by checking the `Argument.IsSpecified` flag. For example, given the above option definition:

    $ tool
    # port = 1234, option.IsSpecified = false
    
    $ tool --server
    # port = 1234, option.IsSpecified = true
    
    $ tool --server 9876
    # port = 9876, option.IsSpecified = true

The syntax used to define the option supports multiple names by separating them
using a pipe. All names are aliases for the same option. For diagnostic
purposes, the first name will be used. By convention that should be the short
name, but it's really up to you.

### Defining parameters

Parameters work in a very similar way:

```C#
Guid guid = Guid.Empty;
syntax.DefineParameter("guid", ref guid, Guid.Parse, "The GUID to use");
```

However, since parameters are matched by position and not by using a named
option, the name is only used for diagnostic purposes and to render a readable
syntax. Hence, they don't support the pipe syntax because having multiple names
wouldn't make any sense there.

Please note that parameters must be specified after options. The reason being
that the parser needs to know all options before it can match parameters.
Otherwise parsing this command would be ambiguous:

    $ tool -x one two

Without knowing whether `-x` takes an argument, it's not clear whether `one`
will be an argument or the first parameter.

### Defining option and parameter lists

Both, options and parameters, support the notion of lists. For example, consider
a compiler `comp`:

    $ comp -D DEBUG -D ARCH=x86 source1 source2 -out:hello

You would define the options and parameters as follows:

```C#
string output = string.Empty;
IReadOnlyList<string> defines = Array.Empty<string>();
IReadOnlyList<string> sources = Array.Empty<string>();

syntax.DefineOption("out", ref output, "Output name");
syntax.DefineOptionList("D|define", ref defines, "Preprocessor definitions");
syntax.DefineParameterList("source", ref sources, "The source files to compile");
```

In general, you can define multiple option lists but only one parameter list.
The reason being that a parameter list will consume all remaining parameters.
You can define individual parameters and a parameter list but the parameter list
must be after the individual parameters otherwise the individual ones will never
be matched.

### Defining commands

Commands are defined in a similar way to options and parameters. The way they
are associated with options and commands is by order:

```C#
var command = string.Empty;
var prune = false;
var message = string.Empty;
var amend = false;

syntax.DefineCommand("pull", ref command, "Pull from another repo");
syntax.DefineOption("p|prune", ref prune, "Prune branches");

syntax.DefineCommand("commit", ref command, "Committing changes");
syntax.DefineOption("m|message", ref message, "The message to use");
syntax.DefineOption("amend", ref amend, "Amend existing commit");
```

In this case the `pull` command has a `-p` option and the `commit` command has
`-m` and `--amend` options. It's worth noting that you can use the same option
name between different commands as they are logically in different scopes.

In order to check which command was used you've two options. You can either
use the version we used above in which case the `ref` variable passed to
`DefineCommand` will contain the name of the command that was specified. But
you're not limited to just plain strings. For example, this will work as well:

```C#
enum Command { Pull, Commit }

// ...

Command command = Command.Pull;
syntax.DefineCommand("pull", ref command, Command.Pull, "Pull from another repo");
syntax.DefineCommand("commit", ref command, Command.Commit, "Committing changes");
```

### Custom help

By default, `ArgumentSyntax` will display the help and exit when `-?`, `-h` or
`--help` is specified. Some tools perform different actions, for instance `git`,
which displays the help on the command line when `-h` is used but opens the web
browser when `--help` is used.

You can support this by handling help yourself:

```C#
ArgumentSyntax.Parse(args, syntax =>
{
    // Turn off built-in help processing

    syntax.HandleHelp = false;

    // Define your own options

    syntax.DefineOption("n|name", ref addressee, "The addressee to greet");

    // Define custom help options. Optionally, you can hide those options
    // from the help text.

    var longHelp = syntax.DefineOption("help", false);
    longHelp.IsHidden = true;

    var quickHelp = syntax.DefineOption("h", false);
    quickHelp.IsHidden = true;

    if (longHelp.Value)
    {
        // Open a browser
        var url = "https://en.wikipedia.org/wiki/%22Hello,_World!%22_program";
        Process.Start(url);
        Environment.Exit(0);
    }
    else if (quickHelp.Value)
    {
        // Show help text. Even if you disable built-in help processing
        // you can still use the built-in help page generator. Optionally,
        // you can ask it to word wrap it according to some maximum, such
        // as the width of the console window.
        var maxWidth = Console.WindowWidth - 2;
        var helpText = syntax.GetHelpText(maxWidth);
        Console.WriteLine(helpText);
        Environment.Exit(0);
    }
});
```

### Additional validation

Let's say you want to perform additional validation, such as that supplied
arguments point to valid files or that certain options aren't used in
combination. You can do this by simply adding a bit of validation code at
the end of the `Parse` method that calls `ReportError`.

```C#
ArgumentSyntax.Parse(args, syntax =>
{
    syntax.DefineOption("n|name", ref addressee, "The addressee to greet");

    if (addressee.Any(char.IsWhiteSpace))
        syntax.ReportError("addressee cannot contain whitespace");
});
```

Usage will look like this:

```
$ ./hello -n "Immo Landwerth"
error: addressee cannot contain whitespace
```

### Custom error handling

There are cases where you want to use `ArgumentSyntax` in such a way that user
errors shouldn't result in the process being terminated. You can do this by
disabling the built-in error handling. In that case, the `ReportError` method --
and thus the `Parse` method -- will throw `ArgumentSyntaxException`.

```C#
try
{
    ArgumentSyntax.Parse(args, syntax =>
    {
        syntax.HandleErrors = false;
        syntax.DefineOption("n|name", ref addressee);
    });
}
catch (ArgumentSyntaxException ex)
{
    Console.WriteLine("Ooops... something didn't go well.");
    Console.WriteLine(ex.Message);
    return 1;
}
```

### Accessing options and parameters

The `ArgumentSyntax` object also provides access to the defined options and
parameters. The `Parse` method returns the used instance, so you can use that
to access them. You can either access all of the defined ones or you can access
only the ones that are relevant to the currently active command.

```C#
static void Main(string[] args)
{
    var addressee = "world";

    var result = ArgumentSyntax.Parse(args, syntax =>
    {
        syntax.DefineOption("n|name", ref addressee, "The addressee to greet");

        if (addressee.Any(char.IsWhiteSpace))
            syntax.ReportError("adressee cannot contain whitespace");
    });

    foreach (var argument in result.GetActiveArguments())
    {
        if (argument.IsOption)
        {
            var names = string.Join(", ", argument.GetDisplayNames());
            Console.WriteLine("Option {0}", names);
        }
        else
        {
            Console.WriteLine("Parameter {0}", argument.GetDisplayName());
        }

        Console.WriteLine("Help         : {0}", argument.Help);
        Console.WriteLine("IsHidden     : {0}", argument.IsHidden);
        Console.WriteLine("Value        : {0}", argument.Value);
        Console.WriteLine("DefaultValue : {0}", argument.DefaultValue);
        Console.WriteLine("IsSpecified  : {0}", argument.IsSpecified);
    }
}
```

### Turning off response files

In case you don't want consumers to use response files or you need to process
parameters that could be prefixed with an `@`-sign, you can disable response
file expansion:

```C#
ArgumentSyntax.Parse(args, syntax =>
{
    syntax.HandleResponseFiles = false;

    // ...
});
```
