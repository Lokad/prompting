# Lokad.Prompting

LLM utilities in a C#/.NET 5 library.

Semantic kernels offer higher-level operations leveraging LLMs as 
lower-level primitives.

**Isoline transducer:** Apply a line-isomorphic tranformation process,
through a linear progressiong through the document. This works well
with Markdown documents (where paragraphs stay on 1 line).

## Notable dependencies

- https://www.nuget.org/packages/Azure.AI.OpenAI/ (Azure OpenAI client)
- https://github.com/dmitry-brazhenko/SharpToken (used to count tokens)

## Isoline transducer examples

### Translate Hugo/Markdown pages

```
Continue the following translation from English to French.
The output may not be starting at the same place than the input.
Preserve YAML front matter, don't touch the '---' delimiters.
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
