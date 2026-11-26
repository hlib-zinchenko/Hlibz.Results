; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
HRES001 | Hlibz.Results | Error | Error type isn't reachable by the generated manifest
HRES002 | Hlibz.Results | Error | Error type has no [Error] attribute
HRES003 | Hlibz.Results | Error | Error code is used twice
HRES004 | Hlibz.Results | Error | Error set isn't a union of error types
HRES005 | Hlibz.Results | Error | Error isn't in the result's error set
HRES006 | Hlibz.Results | Error | Error attribute is invalid
