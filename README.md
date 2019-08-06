# Razor Component Testing Library
Testing library for Razor Components, that allows you to define your component under test and the expected output HTML
in a `.razor` file. It will automatically compare the input with the expected output using the 
[XMLDiff](https://www.xmlunit.org/) library and pretty print error messages using the 
[Shouldly](https://github.com/shouldly/shouldly) library.

The library is currently tied to [xUnit](https://xunit.net/), but it is on the TODO list to make it compatible with all
.NET Core testing libraries.

### Help and input wanted
This is still early days for the library and nothing is set in stone with regards to syntax and functionality.
If you have an idea, suggestion, or bug, please add an [issue](issues). Pull-requests are also very welcome.

## Getting started
1. Install the [Razor.Components.Testing.Library](https://www.nuget.org/packages/Razor.Components.Testing.Library) library from Nuget into your xUnit test project (https://www.nuget.org/packages/Razor.Components.Testing.Library).
2. Optionally, add an `_Imports.razor` to test project to avoid typing using and inherits statements in each test files.
3. Write `.razor`-based tests.

### Example \_Imports.razor
```cshtml
@inherits Egil.RazorComponents.Testing.RazorComponentTest

@using Microsoft.Extensions.DependencyInjection
@using Microsoft.JSInterop
@using Xunit
@using Moq
@using Shouldly
@using Egil.RazorComponents.Testing
```

## Example
The test example below uses the `[Alert](sample/ComponentLib/Alert.razor)` sample component in the sample folder:

https://github.com/egil/razor-component-testing-library/blob/2ad622c9ad0aa21a4454d8d4b681944c3c2e3f0a/sample/ComponentLib/Alert.razor

