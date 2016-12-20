# Ifp.Analyzers
This projects contains a C# diagnostic analyzer that finds getter only properties with backing readonly field and converts them to getter-only auto-properties.
## What it does
This analyzer is inspired by the [Use Getter-Only Auto-Property](https://github.com/DustinCampbell/CSharpEssentials#use-getter-only-auto-property) in the [C# Essentials](https://github.com/DustinCampbell/CSharpEssentials)  analyzer extension by [Dustin Campbell](https://github.com/DustinCampbell). It provides a code fix for a constructor injected services that are made accessible by a getter only property backed by a readonly field.

Before:
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

After:
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

## Conditions

The analyzer is applied to code pattern that meet these criteria. 

* There must be a getter only property with a simple `return` statement
* The `return` statement must reference a `private readonly` field defined in the same type
* The type of the field must match the type of the property

The `same type` rule prevents breaking changes that might be caused by references to the backing field like this one:
```cs
    class ViewModel
    {
        private readonly DelegateCommand _Command; 
        public ViewModel()
        {
            this._Command = new DelegateCommand(()=> { }); 
        }

        public ICommand Command // <- ICommand and DelegateCommand types are different.
        {
            get
            {
                return this._Command;
            }
        }

        void AfterSomeUserAction()
        {
            _Command.RaiseCanExecuteChanged(); // <- RaiseCanExecuteChanged is not defined for ICommand
        }
    }
```

## Field initializer 

Field initializer will be appended to the property.

Before:
```cs
        readonly Dictionary<int, string> _Dict = new Dictionary<int, string>();
        ...
        public Dictionary<int, string> Dict { get { return _Dict; } }
```

After:
```cs
        public Dictionary<int, string> Dict { get; } = new Dictionary<int, string>();
```

## Limitations and pitfalls

There are some glitches with the code fixer you should be aware of.

### Only assignments to the backing fields are renamed

The fixer renames assignments in the constructor to the backing field to the property name. Other references to the backing are **not** renamed. After the fix is applied the compiler error `CS0103 The name does not exist in the current context` reports those missing renames.
```cs
    class AwesomeService
    {
        private readonly ILogger _Logger;

        public AwesomeService(ILogger logger)
        {
            _Logger = logger; // <- _Logger becomes Logger. Assignments to the backing field are renamed.
            _Logger.Log("Constructor called"); // <- _Logger this will not be renamed to Logger.
        }

        protected ILogger Logger
        {
            get
            {
                return _Logger;
            }
        }

        public void DoSomethingAwesome()
        {
            _Logger.Log("Something awesome will happen"); // <- _Logger this will not be renamed to Logger.
        }
    }
```
### Name clashes
In the example below the name of the constructor parameter `Logger` matches the name of the property:
```cs
    class AwesomeService
    {
        private readonly ILogger _Logger;

        public AwesomeService(ILogger Logger) // the name of the parameter matches the name of the property
        {
            _Logger = Logger;
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

After the fix the constructor assignment looks like this:
```cs
        public AwesomeService(ILogger Logger)
        {
            Logger = Logger; // <- CS1717 Assignment made to same variable
        }
```

The compiler reports warning `CS1717 Assignment made to same variable`. This can be fixed by renaming the constructor parameter or by prepending `this.` to the property reference.

### Batch fixer

![Batch fixer](/Artefacts/DocumentationFiles/Batchfixer.png)

The default batch fixer for the *document*, *project* and *solution* struggles to fix several properties at a time per class.
Therefore a custom *FixAllProvider* was written. This *provider* is slow but can fix all o. 

## Installation

The analyzer is available as a Visual Studio 2015 extension and can be found at [https://marketplace.visualstudio.com/vsgallery/9daec1e5-ff49-4d52-a036-f2c0d06cf077](https://marketplace.visualstudio.com/vsgallery/9daec1e5-ff49-4d52-a036-f2c0d06cf077). To install the extension from Visual Studio search for *Diagnostic analyzer for getter only auto props* in the *Extensions and updates* dialog.

## Licence

Licensed under the [Apache License, Version 2.0](http://www.apache.org/licenses/LICENSE-2.0.html)

## Version History

### 1.1 2016-12-20

#### Bug fixes

* Explicit property implementation are ignored
* Field initializer with several [declarators](https://msdn.microsoft.com/en-us/library/aa664742(v=vs.71).aspx) are handled correctly (e.g. `readonly int _Property1, _Property2 = int.MaxValue;`)
* Performance: Analyzer is now applied to properties instead of classes
* Perfromance: Syntaxtree transformation now uses "TrackNodes" for the transformation steps

#### New features

* Custom FixAllProvider introduced

### 1.0 2016-12-09

* Initial publication