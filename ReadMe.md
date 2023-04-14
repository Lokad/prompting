# Lokad.Prompting

Simple kernels for LLMs in a C#/.NET 5 library.

Semantic kernels offer higher-level operations leveraging LLMs as 
lower-level primitives.

**Transducer:** Apply an general transformation to an arbitrarily
long document, through a linear progressiong through the document.
This works well with HTML document.

**Isoline transducer:** Apply a line-isomorphic tranformation process,
through a linear progressiong through the document. This works well
with Markdown documents (where paragraphs stay on 1 line).

## Notable dependencies

- https://github.com/OkGoDoIt/OpenAI-API-dotnet (SDK for OpenAI)
- https://github.com/dmitry-brazhenko/SharpToken (used to count tokens)

## Transducer examples

The transducer comes with to fields namely `{{input}}` and `{{output}}`.


### Markdown-ification of emails

```
Continue the following conversion from HTML to Markdown.
The output may not be starting at the same place than the input.
For images use the markdown syntax `![]()` but preserve the exact
file path as found in the original HTML.
============== RAW EMAIL HTML INPUT ==============
{{input}}
============== EMAIL MARKDOWN OUTPUT ==============
{{output}}
```

### .vtt clean-up (MS Teams audio transcripts)

```
Continue the following conversion from .VTT to Markdown.
The output may not be starting at the same place than the input.
The audio transcript quality of the .VTT file is poor. 
Produce a higher quality edited version.
Remove oral hesitations.
Reduce the chitchat and neduce the number of transitions between people.
Rephrase "oral" segment in the way they would be written instead.
Make the back-and-forth replies bigger than they were.
============== RAW .VTT INPUT ==============
{{input}}
============== MARKDOWN OUTPUT ==============
{{output}}
```

## Transducer examples

### Translate Hugo/Markdown pages

TODO: not working well, isoline transducer needed

```
Continue the following translation from English to French.
The output may not be starting at the same place than the input.
Preserve TOML front matter, don't touch the '---' delimiters.
Do not translate the keys in the TOML header.
Preserve all the Markdown syntax. 
Do not skip images, such as `![Blah blah](/my-image.jpg)`.
Do not touch filenames (ex: `/my-image.jpg`).
Keep prefix line numbers untouched (ex: L123).
Keep blank lines untouched.
Keep line breaks untouched. 
Don't introduce extra line breaks, don't remove them either.

!=!=! ENGLISH INPUT !=!=!
{{input}}

!=!=! FRENCH OUTPUT !=!=!
{{output}}
```

## References

See also
- https://github.com/microsoft/semantic-kernel 
- https://github.com/openai/tiktoken (SharpToken is a port)

## To set the OpenAI API key (dev environment for unit tests)

```powershell
cd test\Lokad.Prompting.Tests
dotnet user-secrets set "OpenAIKey" "sk-...94k0"
```
