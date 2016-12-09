# Ifp.Analyzers
This projects contains a C# diagnostic analyzer that finds getter only properties with backing readonly field and converts them to getter-only auto-properties.
## What it does
This analyzer is inspired by the [C# Essetntials](https://github.com/DustinCampbell/CSharpEssentials) [Use Getter-Only Auto-Property](https://github.com/DustinCampbell/CSharpEssentials#use-getter-only-auto-property) analyzer by [Dustin Campbell](https://github.com/DustinCampbell). It provides a code fix for a constructor injected services that are made accessible by a getter only property backed by a readonly field.
The analyser finds this pattern:
```cs
    class AwesomeService
    {
        private readonly ILogger _Logger;
        public AwesomeService(ILogger logger)
        {
            _Logger = logger;
        }

        protected ILogger Logger
        {
            get
            {
                return _Logger;
            }
        }
    }
```
and fixes is to this:
```cs
    class AwesomeService
    {
        public AwesomeService(ILogger logger)
        {
            Logger = logger;
        }

        protected ILogger Logger { get; }
    }
```
![Sample](/Artefacts/DocumentationFiles/Animation.gif)
### Samples

### Limitations and pitfalls
## Installation
