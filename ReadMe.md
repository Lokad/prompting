# Lokad.Prompting

Simple kernels for LLMs in a C#/.NET 5 library.

Semantic kernels offer higher-level operations leveraging LLMs as 
lower-level primitives.

**Transducer:** Apply an general transformation to an arbitrarily
long document, through a linear progressiong through the document.

TODO: the token management is haphazard 'SharpToken' should be used:
https://github.com/dmitry-brazhenko/SharpToken 

TODO: use case, clean-up a messy .vtt file

## Notable dependencies

- https://github.com/OkGoDoIt/OpenAI-API-dotnet (SDK for OpenAI)

## References

See also https://github.com/microsoft/semantic-kernel 

## To set the OpenAI API key (dev environment for unit tests)

```powershell
cd test\Lokad.Prompting.Tests
dotnet user-secrets set "OpenAIKey" "sk-...94k0"
```
