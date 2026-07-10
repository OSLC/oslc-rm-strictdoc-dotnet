---
url: "https://github.com/OSLC/oslc4net/pull/389"
title: "feat: auto-detect URI collection shape as Resource by ZUOXIANGE · Pull Request #389 · OSLC/oslc4net · GitHub"
fetched_at: "2026-06-23T17:55:57.199738+00:00"
status: 200
content_type: "text/html; charset=utf-8"
---

# feat: auto-detect URI collection shape as Resource by ZUOXIANGE · Pull Request #389 · OSLC/oslc4net · GitHub

feat: auto-detect URI collection shape as Resource by ZUOXIANGE · Pull Request #389 · OSLC/oslc4net
· GitHub Skip to content Navigation Menu Toggle navigation Sign in Appearance settings Platform AI
CODE CREATION GitHub Copilot Write better code with AI GitHub Copilot app Direct agents from issue
to merge MCP Registry New Integrate external tools DEVELOPER WORKFLOWS Actions Automate any workflow
Codespaces Instant dev environments Issues Plan and track work Code Review Manage code changes
APPLICATION SECURITY GitHub Advanced Security Find and fix vulnerabilities Code security Secure your
code as you build Secret protection Stop leaks before they start EXPLORE Why GitHub Documentation
Blog Changelog Marketplace View all features Solutions BY COMPANY SIZE Enterprises Small and medium
teams Startups Nonprofits BY USE CASE App Modernization DevSecOps DevOps CI/CD View all use cases BY
INDUSTRY Healthcare Financial services Manufacturing Government View all industries View all
solutions Resources EXPLORE BY TOPIC AI Software Development DevOps Security View all topics EXPLORE
BY TYPE Customer stories Events & webinars Ebooks & reports Business insights GitHub Skills SUPPORT
& SERVICES Documentation Customer support Community forum Trust center Partners View all resources
Open Source COMMUNITY GitHub Sponsors Fund open source developers PROGRAMS Security Lab Maintainer
Community Accelerator GitHub Stars Archive Program REPOSITORIES Topics Trending Collections
Enterprise ENTERPRISE SOLUTIONS Enterprise platform AI-powered developer platform AVAILABLE ADD-ONS
GitHub Advanced Security Enterprise-grade security features Copilot for Business Enterprise-grade AI
features Premium Support Enterprise-grade 24/7 support Pricing Search or jump to...

Search code, repositories, users, issues, pull requests...

Search Clear Search syntax tips Provide feedback We read every piece of feedback, and take your
input very seriously.

Include my email address so I can be contacted Cancel Submit feedback Saved searches Use saved
searches to filter your results more quickly Name Query To see all available qualifiers, see our
documentation .

Cancel Create saved search Sign in Sign up Appearance settings Resetting focus You signed in with
another tab or window.

Reload to refresh your session.

You signed out in another tab or window.

Reload to refresh your session.

You switched accounts on another tab or window.

Reload to refresh your session.

Dismiss alert {{ message }} OSLC / oslc4net Public Notifications You must be signed in to change
notification settings Fork 12 Star 19 Code Issues 24 Pull requests 7 Discussions Actions Projects
Security and quality 0 Insights Additional navigation options Code Issues Pull requests Discussions
Actions Projects Security and quality Insights feat: auto-detect URI collection shape as Resource #
389 Merged berezovskyi merged 16 commits into OSLC:main OSLC/oslc4net:main from ZUOXIANGE:main Copy
head branch name to clipboard Nov 29, 2025 Conversation Commits 16 ( 16 ) Checks Files changed
Merged feat: auto-detect URI collection shape as Resource # 389 berezovskyi merged 16 commits into
OSLC:main OSLC/oslc4net:main from ZUOXIANGE:main Copy head branch name to clipboard Conversation
ZUOXIANGE commented Aug 15, 2025 • edited by coderabbitai Bot Loading Uh oh!

There was an error while loading.

Please reload this page .

Copy link Copy Markdown Contributor When a property is of a URI collection type
(ICollection/IEnumerable/ISet variants), we can treat is a resource type property for the OSLC
Shapes purposes.

Without it, lacking a ValueType annotation, the following error is reported: CreateResourceShape
TYPE_TO_VALUE_TYPE KeyNotFound Adding comprehensive tests for resource-shape creation covering URI
collection mappings, arrays/lists/sets, getter/setter patterns, and type resolution.

Summary by CodeRabbit Release Notes New Features Enhanced support for URI collection properties to
be properly recognized as resources in resource shapes.

Tests Added comprehensive test suite for resource shape factory with coverage for collection and
property patterns.

Documentation Added setup instructions for Git pre-commit formatting workflow (macOS, Linux, and
Windows).

Chores Added test project infrastructure and updated project references. ✏️ Tip: You can customize
this high-level summary in your review settings.

Sorry, something went wrong.

Uh oh!

There was an error while loading.

Please reload this page .

All reactions fix: CreateResourceShape TYPE_TO_VALUE_TYPE KeyNotFound e9042f1 coderabbitai Bot
commented Aug 15, 2025 • edited Loading Uh oh!

There was an error while loading.

Please reload this page .

Copy link Copy Markdown Contributor Walkthrough Adds three collection-of-Uri entries to
ResourceShapeFactory's type-to-value-type mapping, adds a new .NET test project and solution entry,
and introduces comprehensive ResourceShapeFactory unit tests and supporting test resource classes
covering various URI collection/property patterns.

Changes Cohort / File(s) Summary Core Type Mappings
OSLC4Net_SDK/OSLC4Net.Core/Model/ResourceShapeFactory.cs Adds three entries to the
TYPE_TO_VALUE_TYPE static dictionary mapping ICollection<Uri> , IEnumerable<Uri> , and ISet<Uri> to
ValueType.Resource so collection-of-Uri component types resolve as Resource in default value-type
resolution.

Test Project Setup OSLC4Net_SDK/Tests/OSLC4Net.Core.Tests/OSLC4Net.Core.Tests.csproj Adds a new .NET
10.0 test project referencing core projects and declaring package references:
CommunityToolkit.Diagnostics , Microsoft.Extensions.Logging.Abstractions ,
Microsoft.Extensions.Hosting , Microsoft.NET.Test.Sdk , NSubstitute , NSubstitute.Analyzers.CSharp
(PrivateAssets configured), TUnit , and Verify.TUnit .

Test Suite & Resources OSLC4Net_SDK/Tests/OSLC4Net.Core.Tests/ResourceShapeFactoryTests.cs Adds
ResourceShapeFactoryTests with multiple async test methods validating CreateResourceShape for
Requirement type and for various URI collection/property patterns (ICollection, List, array,
HashSet, ISet, getter/setter patterns); includes several public test resource classes.

Solution Configuration OSLC4Net_SDK/OSLC4Net.Core.slnx Registers the new test project
OSLC4Net.Core.Tests/OSLC4Net.Core.Tests.csproj in the solution under the Tests folder.

Estimated code review effort 🎯 3 (Moderate) | ⏱️ ~25 minutes Verify the new TYPE_TO_VALUE_TYPE
entries follow existing type-resolution conventions and do not conflict with other mappings.

Review the tests for correct assertions around ValueType and Occurs, and ensure test resource
classes reflect intended property/accessor patterns.

Confirm test project package/analyzer settings (PrivateAssets) and solution inclusion are correct.

Poem 🐇 I hopped through mappings, three URIs set free, I planted tests beneath a testing tree,
Getters, lists, arrays all join the run, Hashes and sets now play in the sun, A happy
rabbit—resource shapes done! 🥕 Pre-merge checks and finishing touches ❌ Failed checks (1 warning)
Check name Status Explanation Resolution Docstring Coverage ⚠️ Warning Docstring coverage is 4.55%
which is insufficient.

The required threshold is 80.00%.

You can run @coderabbitai generate docstrings to improve docstring coverage. ✅ Passed checks (2
passed) Check name Status Explanation Description Check ✅ Passed Check skipped - CodeRabbit’s high-
level summary is enabled.

Title check ✅ Passed The title 'feat: auto-detect URI collection shape as Resource' clearly and
specifically describes the main change: enabling automatic detection of URI collection properties as
Resource-type in OSLC Shapes. ✨ Finishing touches 📝 Generate docstrings 🧪 Generate unit tests (beta)
Create PR with unit tests Post copyable unit tests in a comment Thanks for using CodeRabbit !

It's free for OSS, and your support helps us grow.

If you like it, consider giving us a shout-out. ❤️ Share X Mastodon Reddit LinkedIn Comment
@coderabbitai help to get the list of available commands and usage tips.

All reactions Sorry, something went wrong.

Uh oh!

There was an error while loading.

Please reload this page . coderabbitai Bot reviewed Aug 15, 2025 View reviewed changes coderabbitai
Bot left a comment Copy link Copy Markdown Contributor There was a problem hiding this comment.

Choose a reason for hiding this comment The reason will be displayed to describe this comment to
others.

Learn more .

Choose a reason Spam Abuse Off Topic Outdated Duplicate Resolved Low Quality Hide comment Actionable
comments posted: 0 🧹 Nitpick comments (3)
OSLC4Net_SDK/Tests/OSLC4Net.Core.Tests/ResourceShapeFactoryTests.cs (3) 8-9 : Use English for all
code comments.

The XML documentation comment is in Chinese.

According to Microsoft Framework design guidelines, code comments should be in English for
consistency and maintainability. /// <summary> - /// 测试ResourceShapeFactory.CreateResourceShape方法 +
/// Tests for the ResourceShapeFactory.CreateResourceShape method /// </summary> 91-91 : Use English
for all code comments.

The inline comment is in Chinese.

Keep all comments in English for consistency. - //
ResourceShapeFactory只处理以Get或Is开头的方法，Requirement类只有从AbstractResourceRecord继承的GetTypes()方法 + //
ResourceShapeFactory only processes methods starting with Get or Is, Requirement class only has
GetTypes() method inherited from AbstractResourceRecord 10-15 : Consider using more descriptive
constant names.

The constants could be more descriptive to better reflect their purpose in the OSLC context. -
private const string BaseUri = "http://example.com"; - private const string ResourceShapesPath =
"resourceShapes"; - private const string ResourceShapePath = "requirement"; + private const string
TestBaseUri = "http://example.com"; + private const string OslcResourceShapesPath =
"resourceShapes"; + private const string RequirementResourceShapePath = "requirement"; 📜 Review
details Configuration used: .coderabbit.yaml Review profile: CHILL Plan: Pro 💡 Knowledge Base
configuration: MCP integration is disabled by default for public repositories Jira integration is
disabled by default for public repositories Linear integration is disabled by default for public
repositories You can enable these sources in your CodeRabbit configuration. 📥 Commits Reviewing
files that changed from the base of the PR and between 2c6244f and e9042f1 . 📒 Files selected for
processing (4) OSLC4Net_SDK/OSLC4Net.Core.sln (3 hunks)
OSLC4Net_SDK/OSLC4Net.Core/Model/ResourceShapeFactory.cs (1 hunks)
OSLC4Net_SDK/Tests/OSLC4Net.Core.Tests/OSLC4Net.Core.Tests.csproj (1 hunks)
OSLC4Net_SDK/Tests/OSLC4Net.Core.Tests/ResourceShapeFactoryTests.cs (1 hunks) 🧰 Additional context
used 📓 Path-based instructions (1) **/*.cs ⚙️ CodeRabbit Configuration File **/*.cs : Review the C#
code against the Microsoft Framework design guidelines as well as 10 guidelines from "Building
Maintainable Software" by Joost Visser.

Point out deviations and improvements.

Files: OSLC4Net_SDK/OSLC4Net.Core/Model/ResourceShapeFactory.cs
OSLC4Net_SDK/Tests/OSLC4Net.Core.Tests/ResourceShapeFactoryTests.cs 🧬 Code Graph Analysis (2)
OSLC4Net_SDK/OSLC4Net.Core/Model/ResourceShapeFactory.cs (1)
OSLC4Net_SDK/OSLC4Net.Core/Model/ValueType.cs (2) ValueType (69-82) ValueType (84-87)
OSLC4Net_SDK/Tests/OSLC4Net.Core.Tests/ResourceShapeFactoryTests.cs (3)
OSLC4Net_SDK/OSLC4Net.Core/Model/ResourceShapeFactory.cs (3) ResourceShapeFactory (26-511)
ResourceShapeFactory (38-57) ResourceShapeFactory (59-61)
OSLC4Net_SDK/OSLC4Net.Domains.RequirementsManagement/Constants.cs (1) RM (27-77)
OSLC4Net_SDK/OSLC4Net.Core/Model/TypeFactory.cs (1) GetName (41-47) 🔇 Additional comments (11)
OSLC4Net_SDK/OSLC4Net.Core/Model/ResourceShapeFactory.cs (1) 56-56 : LGTM!

The mapping addition is correct and addresses the KeyNotFound issue.

The addition of TYPE_TO_VALUE_TYPE[typeof(ICollection<Uri>)] = ValueType.Resource; properly maps URI
collections to resource value types.

This aligns with the existing pattern where Uri is mapped to ValueType.Resource on line 55, and
extends that mapping to collections of URIs, which is semantically correct for OSLC resource
relationships.

OSLC4Net_SDK/OSLC4Net.Core.sln (3) 45-46 : LGTM!

New test project properly added to the solution.

The new test project is correctly defined with the appropriate GUID and path structure following the
established patterns in the solution. 217-228 : LGTM!

Complete configuration coverage for the new test project.

All solution configurations (Debug/Release across Any CPU, Mixed Platforms, and x86) are properly
configured for the new test project, ensuring it builds correctly in all scenarios. 241-241 : LGTM!

Test project properly organized under Tests folder.

The NestedProjects section correctly places the new test project under the Tests solution folder,
maintaining the solution's organizational structure.

OSLC4Net_SDK/Tests/OSLC4Net.Core.Tests/OSLC4Net.Core.Tests.csproj (1) 1-30 : LGTM!

Well-structured test project configuration.

The project file follows .NET 8.0 best practices with: Proper targeting ( net8.0 ) Appropriate
project references to both core library and domain-specific components Comprehensive test package
dependencies including xUnit v3, NSubstitute for mocking, and code coverage tools Correct asset
configurations for analyzers and build-time dependencies The package selection demonstrates good
testing practices with modern tooling.

OSLC4Net_SDK/Tests/OSLC4Net.Core.Tests/ResourceShapeFactoryTests.cs (6) 16-33 : LGTM!

Well-structured basic validation test.

The test follows the AAA pattern correctly and validates the fundamental functionality of resource
shape creation including proper URI construction. 35-51 : LGTM!

Title validation test is well-implemented.

The test properly verifies that the resource shape has the expected title as configured in the
Requirement type's attributes. 53-71 : LGTM!

Describes validation test correctly checks domain URI.

The test properly validates that the resource shape describes the correct OSLC domain URI for
requirements management, using proper string comparison. 73-94 : LGTM!

Properties validation test appropriately checks expected properties.

The test correctly validates that properties are generated and specifically checks for the "type"
property, which is expected from the inheritance hierarchy. 96-118 : LGTM!

Detailed type property validation is thorough.

The test provides comprehensive validation of the type property including its name, value type,
occurrence, and RDF property definition URI, ensuring proper OSLC compliance. 120-138 : LGTM!

Getter method filtering validation is appropriate.

The test correctly verifies that only getter methods are processed by the ResourceShapeFactory,
confirming the expected behavior of filtering out setter methods and other non-getter methods.

Sorry, something went wrong.

Uh oh!

There was an error while loading.

Please reload this page .

All reactions codecov Bot commented Aug 15, 2025 • edited Loading Uh oh!

There was an error while loading.

Please reload this page .

Copy link Copy Markdown Codecov Report ✅ All modified and coverable lines are covered by tests. ✅
Project coverage is 50.50%.

Comparing base ( 19096fd ) to head ( c325de6 ). ⚠️ Report is 1 commits behind head on main.

Additional details and impacted files @@ Coverage Diff @@ # # main #389 +/- ##
========================================== + Coverage 49.21% 50.50% +1.28%
========================================== Files 166 166 Lines 10040 10043 +3 Branches 963 963
========================================== + Hits 4941 5072 +131 + Misses 4891 4733 -158 - Partials
208 238 +30 ☔ View full report in Codecov by Sentry . 📢 Have feedback on the report?

Share it here . 🚀 New features to boost your workflow: ❄️ Test Analytics : Detect flaky tests,
report on failures, and find test suite problems. 📦 JS Bundle Analysis : Save yourself from yourself
by tracking and limiting bundle sizes in JS merges.

All reactions Sorry, something went wrong.

Uh oh!

There was an error while loading.

Please reload this page . berezovskyi commented Aug 15, 2025 • edited Loading Uh oh!

There was an error while loading.

Please reload this page .

Copy link Copy Markdown Member Thanks for the PR, @ZUOXIANGE !

I am away from a computer for a week or so - reviewing from phone.

First, a question: do the tests fail if we comment out your new mapping entry?

I did not see Uri collections tested explicitly.

Second, I think it would be better to map all collection types in a consistent way - did you check
how other backing types than ICollection are handled?

E.g.

List<Uri> , HashSet<Uri> , Uri[] ?

There a bit of Java-style legacy code (most of the code was written by IBMers between 2009-2013 and
I started actively maintaining the lib and to bring into .NET 5+ since late 2023) that only works
well with those concrete types and not with the high-level interfaces (I only started it recently in
#322 ).

See ExtractTypeInformation() in the DotNetRdfHelper class that I introduced.

Third, I started adding support in #322 for props like ImplementedBy instead of GetImplementedBy and
SetImplementedBy.

I think I missed to update resource shape handling for that.

Would be nice to handle that too but we can file a separate issue for that if it is too much work in
this PR.

Finally, we want to be careful with mapping ICollection to Uri* because there are other ways in RDF
for representing collections other than repeating a prop multiple times (see
https://ontola.io/blog/ordered-data-in-rdf ) but we don't need to worry about this now unless you
are modifying code that handles that conversion.

P.S.

Please update XMLDOC comments to be in English only - see good suggestions from Coderabbit for that
under Nitpick Comments.

All reactions Sorry, something went wrong.

Uh oh!

There was an error while loading.

Please reload this page . fix(ResourceShapeFactory): support for URI resource mapping of IEnume… …
427b216 …rable and ISet types ZUOXIANGE commented Aug 16, 2025 Copy link Copy Markdown Contributor
Author While creating the ResourceShape for Requirements, I encountered a KeyNotFoundException that
required immediate attention to keep our development pipeline unblocked.

I've implemented a temporary fix by adding the necessary code to resolve this specific scenario.

I'd greatly appreciate your expertise in reviewing these changes to ensure they align with the
project's architectural standards.

All reactions Sorry, something went wrong.

Uh oh!

There was an error while loading.

Please reload this page . coderabbitai Bot reviewed Aug 16, 2025 View reviewed changes coderabbitai
Bot left a comment Copy link Copy Markdown Contributor There was a problem hiding this comment.

Choose a reason for hiding this comment The reason will be displayed to describe this comment to
others.

Learn more .

Choose a reason Spam Abuse Off Topic Outdated Duplicate Resolved Low Quality Hide comment Actionable
comments posted: 1 🧹 Nitpick comments (6)
OSLC4Net_SDK/Tests/OSLC4Net.Core.Tests/ResourceShapeFactoryTests.cs (6) 1-5 : Organize imports
according to C# conventions.

The using directives should be organized with System namespaces first, followed by third-party
libraries, then project namespaces.

Additionally, there's a missing using System.Linq; import that's required for LINQ operations used
throughout the file. + using System; + using System.Linq; using OSLC4Net.Core.Model; using
OSLC4Net.Core.Attribute; using OSLC4Net.Domains.RequirementsManagement; using Xunit; 14-31 :
Simplify test to focus on single responsibility.

This test validates multiple aspects of the resource shape (non-null checks and URI construction).

Consider splitting into focused tests or removing redundant assertions since the URI construction is
already validated separately. [Fact] - public void
CreateResourceShape_WithRequirementType_ShouldReturnValidResourceShape() + public void
CreateResourceShape_WithRequirementType_ShouldReturnNonNullResourceShape() { // Arrange var
resourceType = typeof(Requirement); // Act var resourceShape =
ResourceShapeFactory.CreateResourceShape( BaseUri, ResourceShapesPath, ResourceShapePath,
resourceType); // Assert Assert.NotNull(resourceShape); - Assert.NotNull(resourceShape.GetAbout());
- Assert.Equal($"{BaseUri}/{ResourceShapesPath}/{ResourceShapePath}",
resourceShape.GetAbout().ToString()); } 133-135 : Test assertion may be too brittle.

Using Assert.Single(properties) assumes exactly one property will always be present.

This could break if the Requirement class gains additional OSLC properties.

Consider using a more specific assertion. - Assert.Single(properties); - Assert.Equal("type",
properties[0].GetName()); + var typeProperties = properties.Where(p => p.GetName() ==
"type").ToList(); + Assert.Single(typeProperties); + Assert.Equal("type",
typeProperties[0].GetName()); 298-332 : Clean up commented code or document test intent.

This test contains extensive commented-out assertions which makes it unclear whether this is work-
in-progress or intentionally demonstrates unsupported functionality.

The comment on Line 298-299 suggests this is intentional, but the commented code should either be
removed or properly documented. // Note: ResourceShapeFactory only supports getter/setter methods,
not direct properties - // Direct property pattern is not supported by ResourceShapeFactory + //
This test verifies that direct property patterns are NOT supported by ResourceShapeFactory [Fact] -
public void CreateResourceShape_WithISetUriProperty_ShouldMapToResourceValueType() + public void
CreateResourceShape_WithISetUriDirectProperty_ShouldNotMapProperty() { // Arrange var resourceType =
typeof(TestResourceWithISetUri); // Act var resourceShape =
ResourceShapeFactory.CreateResourceShape( BaseUri, ResourceShapesPath, ResourceShapePath,
resourceType); // Assert + // Direct properties are not supported - only getter/setter method
patterns var properties = resourceShape.GetProperties(); var uriSetProperty =
properties.FirstOrDefault(p => p.GetName() == "uriSet"); Assert.Null(uriSetProperty); -
//Assert.Equal("uriSet", uriSetProperty.GetName()); - - //var actualValueType =
uriSetProperty.GetValueType(); - //var actualOccurs = uriSetProperty.GetOccurs(); - -
//Assert.NotNull(actualValueType); - //Assert.NotNull(actualOccurs); - - //var expectedValueTypeUri
= new Uri(ValueTypeExtension.ToString(OSLC4Net.Core.Model.ValueType.Resource)); - //var
expectedOccursUri = new Uri(OccursExtension.ToString(OSLC4Net.Core.Model.Occurs.ZeroOrMany)); - -
//Assert.Equal(expectedValueTypeUri, actualValueType); - //Assert.Equal(expectedOccursUri,
actualOccurs); - //Assert.Equal("http://example.com/uriSet",
uriSetProperty.GetPropertyDefinition()?.ToString()); } 392-392 : Remove unnecessary 'this'
qualifier.

The this qualifier is redundant when there's no naming conflict. - this._uriCollection =
uriCollection; + _uriCollection = uriCollection; 421-421 : Use more readable array initialization.

The current initialization new Uri[0] can be replaced with the more readable Array.Empty<Uri>() for
better intent clarity. - private Uri[] _uriArray = new Uri[0]; + private Uri[] _uriArray =
Array.Empty<Uri>(); 📜 Review details Configuration used: .coderabbit.yaml Review profile: CHILL
Plan: Pro 💡 Knowledge Base configuration: MCP integration is disabled by default for public
repositories Jira integration is disabled by default for public repositories Linear integration is
disabled by default for public repositories You can enable these sources in your CodeRabbit
configuration. 📥 Commits Reviewing files that changed from the base of the PR and between e9042f1
and 427b216 . 📒 Files selected for processing (2)
OSLC4Net_SDK/OSLC4Net.Core/Model/ResourceShapeFactory.cs (1 hunks)
OSLC4Net_SDK/Tests/OSLC4Net.Core.Tests/ResourceShapeFactoryTests.cs (1 hunks) 🚧 Files skipped from
review as they are similar to previous changes (1)
OSLC4Net_SDK/OSLC4Net.Core/Model/ResourceShapeFactory.cs 🧰 Additional context used 📓 Path-based
instructions (1) **/*.cs ⚙️ CodeRabbit Configuration File **/*.cs : Review the C# code against the
Microsoft Framework design guidelines as well as 10 guidelines from "Building Maintainable Software"
by Joost Visser.

Point out deviations and improvements.

Files: OSLC4Net_SDK/Tests/OSLC4Net.Core.Tests/ResourceShapeFactoryTests.cs 🧬 Code Graph Analysis (1)
OSLC4Net_SDK/Tests/OSLC4Net.Core.Tests/ResourceShapeFactoryTests.cs (4)
OSLC4Net_SDK/OSLC4Net.Core/Model/ResourceShapeFactory.cs (5) ResourceShapeFactory (26-513)
ResourceShapeFactory (38-59) ResourceShapeFactory (61-63) ValueType (349-359) Occurs (371-381)
OSLC4Net_SDK/OSLC4Net.Domains.RequirementsManagement/Constants.cs (1) RM (27-77)
OSLC4Net_SDK/OSLC4Net.Core/Model/ValueType.cs (1) ValueTypeExtension (59-88)
OSLC4Net_SDK/OSLC4Net.Core/Model/Occurs.cs (1) OccursExtension (38-67) 🔇 Additional comments (5)
OSLC4Net_SDK/Tests/OSLC4Net.Core.Tests/ResourceShapeFactoryTests.cs (5) 8-13 : LGTM!

Well-structured test class.

The class structure follows good testing practices with clear constants and descriptive naming
conventions. 68-68 : Use StringComparison.Ordinal consistently.

Good practice using StringComparison.Ordinal for URI comparisons to avoid culture-specific issues.
187-194 : Excellent pattern for URI comparison validation.

This test properly handles the fact that GetValueType() and GetOccurs() return URIs by using the
extension methods to get expected URI values for comparison.

This pattern should be applied consistently across all similar tests. 337-362 : Verify correct OSLC
attribute usage pattern.

The test class uses proper OSLC attributes and follows the getter/setter pattern correctly.

The implementation properly handles null arrays and uses ISet<Uri> internally while exposing Uri[]
externally, which is a common pattern for OSLC properties. 138-159 : ResourceShapeFactory:
Collection type mappings validated Confirmed that ResourceShapeFactory registers ICollection<Uri> ,
IEnumerable<Uri> , and ISet<Uri> in its TYPE_TO_VALUE_TYPE dictionary, all mapping to
ValueType.Resource .

ResourceShapeFactoryTests include coverage for each of these collection types—verifying both the
mapped value type and the occurrences setting.

No further action needed.

Sorry, something went wrong.

Uh oh!

There was an error while loading.

Please reload this page .

All reactions Comment thread OSLC4Net_SDK/Tests/OSLC4Net.Core.Tests/ResourceShapeFactoryTests.cs
Show resolved Hide resolved Uh oh!

There was an error while loading.

Please reload this page . berezovskyi and others added 13 commits August 21, 2025 11:35 Merge branch
'main' into main 5e30c38 Merge branch 'main' into main 500f928 fix: using order 363c5da Merge branch
'main' of github.com:ZUOXIANGE/oslc4net 0a87acf Merge branch 'main' into main e96eccb Merge branch
'main' into main 208f23a Merge branch 'main' into main 6022ae3 Migrate OSLC4Net SDK solution to SLNX
format & use better healthcheck ( … … 3db2aae …#458 ) Co-authored-by: google-labs-jules[bot]
<161369871+google-labs-jules[bot]@users.noreply.github.com> Co-authored-by: Andrew Berezovskyi
<andriib@kth.se> # Conflicts: # OSLC4Net_SDK/OSLC4Net.Core.sln Merge branch 'main' into main a3141a1
build: add a test project to the new SLNX solution a8975a4 build: target net10.0 … 657a519 Signed-
off-by: Andrew Berezovskyi <andriib@kth.se> Merge remote-tracking branch 'origin/main' into
ZUOXIANGE-main 8590685 test: migrate to TUnit … 8b4d46d Signed-off-by: Andrew Berezovskyi
<andriib@kth.se> coderabbitai Bot reviewed Nov 29, 2025 View reviewed changes coderabbitai Bot left
a comment Copy link Copy Markdown Contributor There was a problem hiding this comment.

Choose a reason for hiding this comment The reason will be displayed to describe this comment to
others.

Learn more .

Choose a reason Spam Abuse Off Topic Outdated Duplicate Resolved Low Quality Hide comment Actionable
comments posted: 1 📜 Review details Configuration used : Path: .coderabbit.yaml Review profile :
CHILL Plan : Pro 📥 Commits Reviewing files that changed from the base of the PR and between 657a519
and 8b4d46d . 📒 Files selected for processing (2)
OSLC4Net_SDK/Tests/OSLC4Net.Core.Tests/OSLC4Net.Core.Tests.csproj (1 hunks)
OSLC4Net_SDK/Tests/OSLC4Net.Core.Tests/ResourceShapeFactoryTests.cs (1 hunks) 🚧 Files skipped from
review as they are similar to previous changes (1)
OSLC4Net_SDK/Tests/OSLC4Net.Core.Tests/ResourceShapeFactoryTests.cs 🧰 Additional context used 📓
Path-based instructions (1) **/*.csproj 📄 CodeRabbit inference engine (AGENTS.md) **/*.csproj :
Enable implicit usings in C# projects Treat NuGet vulnerabilities as errors in the project
configuration Treat C# nullability warnings (CS8600, CS8602, CS8603) as errors Files:
OSLC4Net_SDK/Tests/OSLC4Net.Core.Tests/OSLC4Net.Core.Tests.csproj 🧠 Learnings (5) 📚 Learning:
2025-11-29T12:45:03.329Z Learnt from: CR Repo: OSLC/oslc4net PR: 0 File: AGENTS.md:0-0 Timestamp:
2025-11-29T12:45:03.329Z Learning: Applies to OSLC4Net_SDK/Tests/**/*.cs : Use TUnit and Verify
frameworks for test projects located in `OSLC4Net_SDK/Tests/` Applied to files:
OSLC4Net_SDK/Tests/OSLC4Net.Core.Tests/OSLC4Net.Core.Tests.csproj 📚 Learning:
2025-11-29T12:45:03.329Z Learnt from: CR Repo: OSLC/oslc4net PR: 0 File: AGENTS.md:0-0 Timestamp:
2025-11-29T12:45:03.329Z Learning: Do not modify `OSLC4Net_NETFramework/` directory unless
explicitly instructed, as it contains legacy .NET Framework samples Applied to files:
OSLC4Net_SDK/Tests/OSLC4Net.Core.Tests/OSLC4Net.Core.Tests.csproj 📚 Learning:
2025-11-29T12:45:03.329Z Learnt from: CR Repo: OSLC/oslc4net PR: 0 File: AGENTS.md:0-0 Timestamp:
2025-11-29T12:45:03.329Z Learning: Applies to
{*.sh,*.bat,*.yml,*.yaml,.github/**/*.yml,.github/**/*.yaml} : Run tests from the `OSLC4Net_SDK`
directory or below using: `AGENT_BUILD=true dotnet test --solution OSLC4Net.Core.slnx
--configuration Release` Applied to files:
OSLC4Net_SDK/Tests/OSLC4Net.Core.Tests/OSLC4Net.Core.Tests.csproj 📚 Learning:
2025-11-29T12:45:03.329Z Learnt from: CR Repo: OSLC/oslc4net PR: 0 File: AGENTS.md:0-0 Timestamp:
2025-11-29T12:45:03.329Z Learning: Applies to
{*.sh,*.bat,*.yml,*.yaml,.github/**/*.yml,.github/**/*.yaml} : Filter tests using treenode-filter in
TUnit-based tests with: `AGENT_BUILD=true dotnet test --solution OSLC4Net.Core.slnx --configuration
Release --treenode-filter '/*/*/*/*[TestCategory!=RunningOslcServerRequired]'` Applied to files:
OSLC4Net_SDK/Tests/OSLC4Net.Core.Tests/OSLC4Net.Core.Tests.csproj 📚 Learning:
2025-11-29T12:45:03.329Z Learnt from: CR Repo: OSLC/oslc4net PR: 0 File: AGENTS.md:0-0 Timestamp:
2025-11-29T12:45:03.329Z Learning: Applies to
{*.sh,*.bat,*.yml,*.yaml,.github/**/*.yml,.github/**/*.yaml} : Build the solution using:
`AGENT_BUILD=true dotnet build OSLC4Net_SDK/OSLC4Net.Core.slnx` Applied to files:
OSLC4Net_SDK/Tests/OSLC4Net.Core.Tests/OSLC4Net.Core.Tests.csproj 🔇 Additional comments (3)
OSLC4Net_SDK/Tests/OSLC4Net.Core.Tests/OSLC4Net.Core.Tests.csproj (3) 3-3 : Verify net10.0 target
framework compatibility.

The project targets net10.0 , which is a recent release.

Confirm that: The development environment supports .NET 10.

The target framework aligns with the minimum .NET version required by the solution and its
dependencies (OSLC4Net.Core, OSLC4Net.Domains.RequirementsManagement).

CI/CD pipelines are configured to run tests with .NET 10.

Please verify the target framework is appropriate and supported across the development and CI/CD
environments. 19-20 : TUnit and Verify framework choices align with project standards.

The inclusion of TUnit and Verify.TUnit is consistent with the project's test framework learnings.

This choice supports structured, modern testing patterns as referenced in the retrieved learnings.
10-20 : Based on my verification, I've found critical issues with the review comment: The review
comment is incorrect and based on a fundamental misunderstanding of the project's architecture.

Key findings: File path is wrong : The file
OSLC4Net_SDK/Tests/OSLC4Net.Core.Tests/OSLC4Net.Core.Tests.csproj does not exist in the repository.

The matching code snippet appears to be from
OSLC4Net_SDK/Tests/OSLC4Net.Client.Tests/OSLC4Net.Client.Tests.csproj .

Central package version management is in place : The project uses Directory.Packages.props with
ManagePackageVersionsCentrally=true .

This is the correct modern .NET approach.

Package references in individual .csproj files intentionally omit versions—they inherit from the
central file.

Suggested versions are outdated/incorrect : CommunityToolkit.Diagnostics: Suggested 8.2.2 → Actual:
8.4.0 Microsoft.Extensions.Logging.Abstractions: Suggested 9.0.0 → Actual: 10.0.0
Microsoft.Extensions.Hosting: Suggested 9.0.0 → Actual: 10.0.0 Microsoft.NET.Test.Sdk: Suggested
17.14.0 → Actual: 18.0.0 NSubstitute: Suggested 7.1.2 → Actual: 5.3.0 The current approach is
correct : Omitting explicit versions in .csproj files when using central management is the intended
and correct pattern.

Adding versions would contradict this architecture.

Likely an incorrect or invalid review comment.

Sorry, something went wrong.

Uh oh!

There was an error while loading.

Please reload this page .

All reactions Comment thread OSLC4Net_SDK/Tests/OSLC4Net.Core.Tests/OSLC4Net.Core.Tests.csproj Show
resolved Hide resolved Uh oh!

There was an error while loading.

Please reload this page . chore: format … c325de6 Signed-off-by: Andrew Berezovskyi <andriib@kth.se>
berezovskyi changed the title fix: CreateResourceShape TYPE_TO_VALUE_TYPE KeyNotFound feat: auto-
detect URI collection shape as Resource Nov 29, 2025 coderabbitai Bot reviewed Nov 29, 2025 View
reviewed changes coderabbitai Bot left a comment Copy link Copy Markdown Contributor There was a
problem hiding this comment.

Choose a reason for hiding this comment The reason will be displayed to describe this comment to
others.

Learn more .

Choose a reason Spam Abuse Off Topic Outdated Duplicate Resolved Low Quality Hide comment Actionable
comments posted: 1 ♻️ Duplicate comments (1)
OSLC4Net_SDK/Tests/OSLC4Net.Core.Tests/ResourceShapeFactoryTests.cs (1) 1-6 : Add missing
System.Linq using for LINQ extension methods Any , Select , ToList , and ToArray used later in this
file require System.Linq ; without it the file will not compile (unless there is a project-level
global using, which earlier comments suggest is not the case).

Please add: + using System.Linq; near the top of the file. 🧹 Nitpick comments (3)
OSLC4Net_SDK/Tests/OSLC4Net.Core.Tests/ResourceShapeFactoryTests.cs (3) 138-160 : Strengthen
ICollection<Uri> test to assert Resource / ZeroOrMany explicitly
CreateResourceShape_WithICollectionUriProperty_ShouldMapToResourceValueType currently only checks
that GetValueType() and GetOccurs() are non‑null.

That won’t catch regressions where the mapping is to the wrong value type or occurs.

Consider asserting the concrete URIs, like you do for List<Uri> , Uri[] , and HashSet<Uri> : - await
Assert.That(uriCollectionProperty.GetValueType()).IsNotNull(); - await
Assert.That(uriCollectionProperty.GetOccurs()).IsNotNull(); - await Assert.That(uriCollectionPropert
y.GetPropertyDefinition().ToString()).IsEqualTo("http://example.com/uriCollection"); + var
actualValueType = uriCollectionProperty.GetValueType(); + var actualOccurs =
uriCollectionProperty.GetOccurs(); + + await Assert.That(actualValueType).IsNotNull(); + await
Assert.That(actualOccurs).IsNotNull(); + + var expectedValueTypeUri = new
Uri(ValueTypeExtension.ToString(OSLC4Net.Core.Model.ValueType.Resource)); + var expectedOccursUri =
new Uri(OccursExtension.ToString(OSLC4Net.Core.Model.Occurs.ZeroOrMany)); + + await
Assert.That(actualValueType).IsEqualTo(expectedValueTypeUri); + await
Assert.That(actualOccurs).IsEqualTo(expectedOccursUri); + await Assert.That(uriCollectionProperty.Ge
tPropertyDefinition().ToString()).IsEqualTo("http://example.com/uriCollection"); This aligns the
test with the PR’s intent to treat URI collections as Resource with ZeroOrMany occurs. 299-333 :
Align ISet test name with behavior and remove commented-out assertions This test asserts that
uriSetProperty is null , which matches the comment that direct property patterns are not supported.

However, the method name CreateResourceShape_WithISetUriProperty_ShouldMapToResourceValueType still
suggests a positive mapping and the commented-out assertions below reflect that older expectation.

To reduce confusion and adhere to maintainability guidelines (no large blocks of commented-out
code), consider: Renaming the test to describe the current behavior: - public async Task
CreateResourceShape_WithISetUriProperty_ShouldMapToResourceValueType() + public async Task
CreateResourceShape_WithISetUriProperty_ShouldNotCreatePropertyForDirectPattern() Removing the
now‑irrelevant commented assertions (lines 319–332) or replacing them with a short comment/TODO if
you intend to support direct properties in a future change.

This keeps the tests self‑documenting and avoids stale expectations lingering in comments. 15-297 :
Reduce duplication in Resource/ZeroOrMany assertions and consider Verify for shape snapshots Several
tests ( CreateResourceShape_WithListUriProperty... , ...WithUriArrayProperty... ,
...WithHashSetUriProperty... , and the ICollection test if updated) repeat the same pattern of:
Locating a property by name Asserting ValueType.Resource Asserting Occurs.ZeroOrMany Checking the
property definition URI To keep the tests DRY and easier to extend, consider extracting a small
helper (static method in this class or a separate test helper) that encapsulates these assertions,
for example: private static async Task AssertResourceCollectionProperty ( Property property , string
expectedName , string expectedPropertyDefinition ) { await Assert .

That ( property ) .

IsNotNull ( ) ; await Assert .

That ( property .

GetName ( ) ) .

IsEqualTo ( expectedName ) ; var valueType = property .

GetValueType ( ) ; var occurs = property .

GetOccurs ( ) ; var expectedValueTypeUri = new Uri ( ValueTypeExtension .

ToString ( ValueType .

Resource ) ) ; var expectedOccursUri = new Uri ( OccursExtension .

ToString ( Occurs .

ZeroOrMany ) ) ; await Assert .

That ( valueType ) .

IsEqualTo ( expectedValueTypeUri ) ; await Assert .

That ( occurs ) .

IsEqualTo ( expectedOccursUri ) ; await Assert .

That ( property .

GetPropertyDefinition ( ) ? .

ToString ( ) ) .

IsEqualTo ( expectedPropertyDefinition ) ; } Then each test can just locate the property and call
the helper, improving readability and maintainability.

Based on learnings, if you find yourself expanding these checks further, you might also consider a
Verify snapshot of the full ResourceShape as a complementary higher‑level test. 📜 Review details
Configuration used : Path: .coderabbit.yaml Review profile : CHILL Plan : Pro 📥 Commits Reviewing
files that changed from the base of the PR and between 8b4d46d and c325de6 . 📒 Files selected for
processing (2) AGENTS.md (1 hunks)
OSLC4Net_SDK/Tests/OSLC4Net.Core.Tests/ResourceShapeFactoryTests.cs (1 hunks) 🧰 Additional context
used 📓 Path-based instructions (3) **/*.md ⚙️ CodeRabbit configuration file **/*.md : Review the
documentation against the Microsoft Writing Style Guide and point out any mismatches.

Ensure the Oxford English spelling is adhered to.

Files: AGENTS.md **/*.cs 📄 CodeRabbit inference engine (AGENTS.md) **/*.cs : Use C# 14 with nullable
reference types enabled Follow .NET code analysis and Meziantou.Analyzer rules in C# code Files:
OSLC4Net_SDK/Tests/OSLC4Net.Core.Tests/ResourceShapeFactoryTests.cs ⚙️ CodeRabbit configuration file
**/*.cs : Review the C# code against the Microsoft Framework design guidelines as well as 10
guidelines from "Building Maintainable Software" by Joost Visser.

Point out deviations and improvements.

Files: OSLC4Net_SDK/Tests/OSLC4Net.Core.Tests/ResourceShapeFactoryTests.cs
OSLC4Net_SDK/Tests/**/*.cs 📄 CodeRabbit inference engine (AGENTS.md) Use TUnit and Verify frameworks
for test projects located in OSLC4Net_SDK/Tests/ Files:
OSLC4Net_SDK/Tests/OSLC4Net.Core.Tests/ResourceShapeFactoryTests.cs 🧠 Learnings (3) 📚 Learning:
2025-11-29T12:45:03.329Z Learnt from: CR Repo: OSLC/oslc4net PR: 0 File: AGENTS.md:0-0 Timestamp:
2025-11-29T12:45:03.329Z Learning: Applies to
{*.sh,*.bat,*.yml,*.yaml,.github/**/*.yml,.github/**/*.yaml} : Build the solution using:
`AGENT_BUILD=true dotnet build OSLC4Net_SDK/OSLC4Net.Core.slnx` Applied to files: AGENTS.md 📚
Learning: 2025-11-29T12:45:03.329Z Learnt from: CR Repo: OSLC/oslc4net PR: 0 File: AGENTS.md:0-0
Timestamp: 2025-11-29T12:45:03.329Z Learning: Applies to
{*.sh,*.bat,*.yml,*.yaml,.github/**/*.yml,.github/**/*.yaml} : Run tests from the `OSLC4Net_SDK`
directory or below using: `AGENT_BUILD=true dotnet test --solution OSLC4Net.Core.slnx
--configuration Release` Applied to files: AGENTS.md 📚 Learning: 2025-11-29T12:45:03.329Z Learnt
from: CR Repo: OSLC/oslc4net PR: 0 File: AGENTS.md:0-0 Timestamp: 2025-11-29T12:45:03.329Z Learning:
Applies to OSLC4Net_SDK/Tests/**/*.cs : Use TUnit and Verify frameworks for test projects located in
`OSLC4Net_SDK/Tests/` Applied to files:
OSLC4Net_SDK/Tests/OSLC4Net.Core.Tests/ResourceShapeFactoryTests.cs 🧬 Code graph analysis (1)
OSLC4Net_SDK/Tests/OSLC4Net.Core.Tests/ResourceShapeFactoryTests.cs (4)
OSLC4Net_SDK/OSLC4Net.Core/Model/ResourceShapeFactory.cs (5) ResourceShapeFactory (26-513)
ResourceShapeFactory (38-59) ResourceShapeFactory (61-63) ValueType (349-359) Occurs (371-381)
OSLC4Net_SDK/OSLC4Net.Domains.RequirementsManagement/Constants.cs (1) RM (27-77)
OSLC4Net_SDK/OSLC4Net.Core/Model/ValueType.cs (1) ValueTypeExtension (59-88)
OSLC4Net_SDK/OSLC4Net.Core/Model/Occurs.cs (1) OccursExtension (38-67) 🪛 markdownlint-cli2 (0.18.1)
AGENTS.md 26-26: Unordered list indentation Expected: 2; Actual: 1 (MD007, ul-indent) 26-26: Hard
tabs Column: 1 (MD010, no-hard-tabs) 27-27: Unordered list indentation Expected: 2; Actual: 1
(MD007, ul-indent) 27-27: Hard tabs Column: 1 (MD010, no-hard-tabs) 28-28: Unordered list
indentation Expected: 2; Actual: 1 (MD007, ul-indent) 28-28: Hard tabs Column: 1 (MD010, no-hard-
tabs) 31-31: Unordered list indentation Expected: 2; Actual: 1 (MD007, ul-indent) 31-31: Hard tabs
Column: 1 (MD010, no-hard-tabs) 32-32: Unordered list indentation Expected: 2; Actual: 1 (MD007, ul-
indent) 32-32: Hard tabs Column: 1 (MD010, no-hard-tabs) 33-33: Unordered list indentation Expected:
4; Actual: 2 (MD007, ul-indent) 33-33: Hard tabs Column: 1 (MD010, no-hard-tabs) 34-34: Unordered
list indentation Expected: 4; Actual: 2 (MD007, ul-indent) 34-34: Hard tabs Column: 1 (MD010, no-
hard-tabs) 🔇 Additional comments (1)
OSLC4Net_SDK/Tests/OSLC4Net.Core.Tests/ResourceShapeFactoryTests.cs (1) 9-263 : Overall test
coverage for URI collection shape mapping looks solid The tests exercise
ResourceShapeFactory.CreateResourceShape across: The core Requirement type
(about/title/describes/type property).

Multiple URI collection shapes (ICollection, List, array, HashSet) with correct value type and
occurs semantics.

Getter/setter vs. direct property patterns.

This provides good regression protection for the new TYPE_TO_VALUE_TYPE entries and the
backing‑type/interface resolution logic.

Once the minor issues above are addressed, this suite should serve as a strong safety net for future
changes in this area.

Please run the updated test suite ( dotnet test for the test project) after applying changes to
confirm everything passes as expected.

Sorry, something went wrong.

Uh oh!

There was an error while loading.

Please reload this page .

All reactions Comment thread AGENTS.md Comment on lines +25 to +34 - Hook behavior: - Runs `dotnet
format` inside `OSLC4Net_SDK`. - Stages any whitespace or formatting changes automatically. - Fails
the commit if formatting fails or cannot be applied. - One-time setup: - Use the provided POSIX
shell hook script: `.githooks/pre-commit`. - Point Git to use the repository hooks folder.

Quick setup scripts: - macOS/Linux: `scripts/setup-hooks.sh` - Windows (PowerShell): `scripts/setup-
hooks.ps1` coderabbitai Bot Nov 29, 2025 Copy link Copy Markdown Contributor There was a problem
hiding this comment.

Choose a reason for hiding this comment The reason will be displayed to describe this comment to
others.

Learn more .

Choose a reason Spam Abuse Off Topic Outdated Duplicate Resolved Low Quality Hide comment ⚠️
Potential issue | 🟡 Minor Fix markdown indentation and hard tab violations.

The unordered list uses hard tabs and inconsistent indentation, violating markdownlint rules (MD010
and MD007).

Use 2-space indentation for first-level bullets and 4 spaces for nested items.

Apply this diff: - Hook behavior: - - Runs `dotnet format` inside `OSLC4Net_SDK`. - - Stages any
whitespace or formatting changes automatically. - - Fails the commit if formatting fails or cannot
be applied. - - One-time setup: - - Use the provided POSIX shell hook script: `.githooks/pre-
commit`. - - Point Git to use the repository hooks folder.

Quick setup scripts: - - macOS/Linux: `scripts/setup-hooks.sh` - - Windows (PowerShell):
`scripts/setup-hooks.ps1` + Hook behavior: + - Runs `dotnet format` inside `OSLC4Net_SDK`. + -
Stages any whitespace or formatting changes automatically. + - Fails the commit if formatting fails
or cannot be applied. + + One-time setup: + - Use the provided POSIX shell hook script:
`.githooks/pre-commit`. + - Point Git to use the repository hooks folder.

Quick setup scripts: + - macOS/Linux: `scripts/setup-hooks.sh` + - Windows (PowerShell):
`scripts/setup-hooks.ps1` 📝 Committable suggestion ‼️ IMPORTANT Carefully review the code before
committing.

Ensure that it accurately replaces the highlighted code, contains no missing lines, and has no
issues with indentation.

Thoroughly test & benchmark the code to ensure it meets the requirements.

Suggested change - Hook behavior: - Runs `dotnet format` inside `OSLC4Net_SDK`. - Stages any
whitespace or formatting changes automatically. - Fails the commit if formatting fails or cannot be
applied. - One-time setup: - Use the provided POSIX shell hook script: `.githooks/pre-commit`. -
Point Git to use the repository hooks folder.

Quick setup scripts: - macOS/Linux: `scripts/setup-hooks.sh` - Windows (PowerShell): `scripts/setup-
hooks.ps1` - Hook behavior: - Runs ` dotnet format ` inside ` OSLC4Net_SDK ` . - Stages any
whitespace or formatting changes automatically. - Fails the commit if formatting fails or cannot be
applied. - One-time setup: - Use the provided POSIX shell hook script: ` .githooks/pre-commit ` . -
Point Git to use the repository hooks folder.

Quick setup scripts: - macOS/Linux: `scripts/setup-hooks.sh` - Windows (PowerShell): `scripts/setup-
hooks.ps1` 🧰 Tools 🪛 markdownlint-cli2 (0.18.1) 26-26: Unordered list indentation Expected: 2;
Actual: 1 (MD007, ul-indent) 26-26: Hard tabs Column: 1 (MD010, no-hard-tabs) 27-27: Unordered list
indentation Expected: 2; Actual: 1 (MD007, ul-indent) 27-27: Hard tabs Column: 1 (MD010, no-hard-
tabs) 28-28: Unordered list indentation Expected: 2; Actual: 1 (MD007, ul-indent) 28-28: Hard tabs
Column: 1 (MD010, no-hard-tabs) 31-31: Unordered list indentation Expected: 2; Actual: 1 (MD007, ul-
indent) 31-31: Hard tabs Column: 1 (MD010, no-hard-tabs) 32-32: Unordered list indentation Expected:
2; Actual: 1 (MD007, ul-indent) 32-32: Hard tabs Column: 1 (MD010, no-hard-tabs) 33-33: Unordered
list indentation Expected: 4; Actual: 2 (MD007, ul-indent) 33-33: Hard tabs Column: 1 (MD010, no-
hard-tabs) 34-34: Unordered list indentation Expected: 4; Actual: 2 (MD007, ul-indent) 34-34: Hard
tabs Column: 1 (MD010, no-hard-tabs) 🤖 Prompt for AI Agents In AGENTS.md around lines 25 to 34, the
unordered list uses hard tabs and inconsistent indentation (violating MD010/MD007); replace all hard
tabs with spaces, use 2-space indentation for top-level bullets and 4-space indentation for nested
bullets (e.g., first-level items start with two spaces then "- ", nested items start with four
spaces then "- "), and ensure consistent spacing throughout so markdownlint passes.

Sorry, something went wrong.

Uh oh!

There was an error while loading.

Please reload this page .

All reactions Hide details View details berezovskyi merged commit bc9f024 into OSLC : main Nov 29,
2025 10 checks passed Uh oh!

There was an error while loading.

Please reload this page . berezovskyi commented Nov 29, 2025 Copy link Copy Markdown Member
@ZUOXIANGE thanks for the PR, I had to make a few updates to it, but now it is finally merged.

Two points: You should create a branch from forked main and submit that as a PR.

As you saw, my pushes tried to trigger main-branch actions, which should not run on forks.

You should delete your main branch and sync it with our main again.

If you have a custom fork need, you can create something like main-fork branch.

This way, you will easily be able to sync your main with our main.

Then you can merge your main into main-fork to avoid drifting out of sync.

Again, thanks for a contribution!

All reactions Sorry, something went wrong.

Uh oh!

There was an error while loading.

Please reload this page . coderabbitai Bot mentioned this pull request Feb 14, 2026 Fix obsolete
GetRdfTypes usage in samples #608 Merged This file contains hidden or bidirectional Unicode text
that may be interpreted or compiled differently than what appears below.

To review, open the file in an editor that reveals hidden Unicode characters.

Learn more about bidirectional Unicode characters Show hidden characters Sign up for free to join
this conversation o
