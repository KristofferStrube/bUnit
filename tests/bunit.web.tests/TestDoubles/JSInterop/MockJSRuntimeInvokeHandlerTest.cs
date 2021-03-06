using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.JSInterop;
using Shouldly;
using Xunit;

namespace Bunit.TestDoubles.JSInterop
{
	public class MockJSRuntimeInvokeHandlerTest
	{
		[Fact(DisplayName = "Mock returns default value in loose mode without invocation setup")]
		public async Task Test001()
		{
			var sut = new MockJSRuntimeInvokeHandler(JSRuntimeMockMode.Loose);

			var result = await sut.ToJSRuntime().InvokeAsync<object>("ident", Array.Empty<object>());

			result.ShouldBe(default);
		}

		[Fact(DisplayName = "After invocation a invocation should be visible from the Invocations list")]
		public void Test002()
		{
			var identifier = "fooFunc";
			var args = new[] { "bar", "baz" };
			using var cts = new CancellationTokenSource();
			var sut = new MockJSRuntimeInvokeHandler(JSRuntimeMockMode.Loose);

			var _ = sut.ToJSRuntime().InvokeAsync<object>(identifier, cts.Token, args);

			var invocation = sut.Invocations[identifier].Single();
			invocation.Identifier.ShouldBe(identifier);
			invocation.Arguments.ShouldBe(args);
			invocation.CancellationToken.ShouldBe(cts.Token);
		}

		[Fact(DisplayName = "Mock throws exception when in strict mode and invocation has not been setup")]
		public async Task Test003()
		{
			var sut = new MockJSRuntimeInvokeHandler(JSRuntimeMockMode.Strict);
			var identifier = "func";
			var args = new[] { "bar", "baz" };

			var exception = await Should.ThrowAsync<UnplannedJSInvocationException>(sut.ToJSRuntime().InvokeVoidAsync(identifier, args).AsTask());
			exception.Invocation.Identifier.ShouldBe(identifier);
			exception.Invocation.Arguments.ShouldBe(args);

			exception = Should.Throw<UnplannedJSInvocationException>(() => { var _ = sut.ToJSRuntime().InvokeAsync<object>(identifier, args); });
			exception.Invocation.Identifier.ShouldBe(identifier);
			exception.Invocation.Arguments.ShouldBe(args);
		}

		[Fact(DisplayName = "All invocations received AFTER a planned invocation " +
							"has a result set, receives the same result")]
		public async Task Test005x()
		{
			var identifier = "func";
			var expectedResult = Guid.NewGuid();
			var sut = new MockJSRuntimeInvokeHandler(JSRuntimeMockMode.Strict);
			var jsRuntime = sut.ToJSRuntime();
			var plannedInvoke = sut.Setup<Guid>(identifier);

			plannedInvoke.SetResult(expectedResult);

			var i1 = jsRuntime.InvokeAsync<Guid>(identifier);
			var i2 = jsRuntime.InvokeAsync<Guid>(identifier);

			(await i1).ShouldBe(expectedResult);
			(await i2).ShouldBe(expectedResult);
		}

		[Fact(DisplayName = "All invocations received BEFORE a planned invocation " +
							"has a result set, receives the same result")]
		public async Task Test005()
		{
			var identifier = "func";
			var expectedResult = Guid.NewGuid();
			var sut = new MockJSRuntimeInvokeHandler(JSRuntimeMockMode.Strict);
			var jsRuntime = sut.ToJSRuntime();
			var plannedInvoke = sut.Setup<Guid>(identifier);

			var i1 = jsRuntime.InvokeAsync<Guid>(identifier);
			var i2 = jsRuntime.InvokeAsync<Guid>(identifier);

			plannedInvoke.SetResult(expectedResult);

			(await i1).ShouldBe(expectedResult);
			(await i2).ShouldBe(expectedResult);
		}

		[Fact(DisplayName = "Invocations receive the latest result set in a planned invocation")]
		public async Task Test006x()
		{
			var identifier = "func";
			var sut = new MockJSRuntimeInvokeHandler(JSRuntimeMockMode.Strict);
			var plannedInvoke = sut.Setup<Guid>(identifier);
			var jsRuntime = sut.ToJSRuntime();

			var expectedResult1 = Guid.NewGuid();
			plannedInvoke.SetResult(expectedResult1);
			var i1 = jsRuntime.InvokeAsync<Guid>(identifier);

			var expectedResult2 = Guid.NewGuid();
			plannedInvoke.SetResult(expectedResult2);
			var i2 = jsRuntime.InvokeAsync<Guid>(identifier);

			(await i1).ShouldBe(expectedResult1);
			(await i2).ShouldBe(expectedResult2);
		}

		[Fact(DisplayName = "A planned invocation can be canceled for any waiting received invocations")]
		public void Test007()
		{
			var identifier = "func";
			var sut = new MockJSRuntimeInvokeHandler(JSRuntimeMockMode.Strict);
			var plannedInvoke = sut.Setup<Guid>(identifier);
			var invocation = sut.ToJSRuntime().InvokeAsync<Guid>(identifier);

			plannedInvoke.SetCanceled();

			invocation.IsCanceled.ShouldBeTrue();
		}

		[Fact(DisplayName = "A planned invocation can throw an exception for any waiting received invocations")]
		public async Task Test008()
		{
			var identifier = "func";
			var sut = new MockJSRuntimeInvokeHandler(JSRuntimeMockMode.Strict);
			var plannedInvoke = sut.Setup<Guid>(identifier);
			var invocation = sut.ToJSRuntime().InvokeAsync<Guid>(identifier);
			var expectedException = new InvalidOperationException("TADA");

			plannedInvoke.SetException(expectedException);

			var actual = await Should.ThrowAsync<InvalidOperationException>(invocation.AsTask());
			actual.ShouldBe(expectedException);
			invocation.IsFaulted.ShouldBeTrue();
		}

		[Fact(DisplayName = "Invocations returns all from a planned invocation")]
		public void Test009()
		{
			var identifier = "func";
			var sut = new MockJSRuntimeInvokeHandler(JSRuntimeMockMode.Strict);
			var plannedInvoke = sut.Setup<Guid>(identifier, x => true);
			var i1 = sut.ToJSRuntime().InvokeAsync<Guid>(identifier, "first");
			var i2 = sut.ToJSRuntime().InvokeAsync<Guid>(identifier, "second");

			var invocations = plannedInvoke.Invocations;

			invocations.Count.ShouldBe(2);
			invocations[0].Arguments[0].ShouldBe("first");
			invocations[1].Arguments[0].ShouldBe("second");
		}

		[Fact(DisplayName = "Arguments used in Setup are matched with invocations")]
		public void Test010()
		{
			var sut = new MockJSRuntimeInvokeHandler(JSRuntimeMockMode.Strict);
			var planned = sut.Setup<object>("foo", "bar", 42);

			var _ = sut.ToJSRuntime().InvokeAsync<object>("foo", "bar", 42);

			Should.Throw<UnplannedJSInvocationException>(() => { var _ = sut.ToJSRuntime().InvokeAsync<object>("foo", "bar", 41); });

			planned.Invocations.Count.ShouldBe(1);
			var invocation = planned.Invocations[0];
			invocation.Identifier.ShouldBe("foo");
			invocation.Arguments[0].ShouldBe("bar");
			invocation.Arguments[1].ShouldBe(42);
		}

		[Fact(DisplayName = "Argument matcher used in Setup are matched with invocations")]
		public void Test011()
		{
			var sut = new MockJSRuntimeInvokeHandler(JSRuntimeMockMode.Strict);
			var planned = sut.Setup<object>("foo", args => args.Count == 1);

			var _ = sut.ToJSRuntime().InvokeAsync<object>("foo", 42);

			Should.Throw<UnplannedJSInvocationException>(() => { var _ = sut.ToJSRuntime().InvokeAsync<object>("foo", "bar", 42); });

			planned.Invocations.Count.ShouldBe(1);
			var invocation = planned.Invocations[0];
			invocation.Identifier.ShouldBe("foo");
			invocation.Arguments.Count.ShouldBe(1);
			invocation.Arguments[0].ShouldBe(42);
		}

		[Fact(DisplayName = "SetupVoid returns a planned invocation that does not take a result object")]
		public async Task Test012()
		{
			var identifier = "func";
			var sut = new MockJSRuntimeInvokeHandler(JSRuntimeMockMode.Strict);
			var plannedInvoke = sut.SetupVoid(identifier);

			var invocation = sut.ToJSRuntime().InvokeVoidAsync(identifier);
			plannedInvoke.SetVoidResult();

			await invocation;

			invocation.IsCompletedSuccessfully.ShouldBeTrue();
		}

		[Fact(DisplayName = "Arguments used in SetupVoid are matched with invocations")]
		public async Task Test013()
		{
			var sut = new MockJSRuntimeInvokeHandler(JSRuntimeMockMode.Strict);
			var planned = sut.SetupVoid("foo", "bar", 42);

			var _ = sut.ToJSRuntime().InvokeVoidAsync("foo", "bar", 42);

			await Should.ThrowAsync<UnplannedJSInvocationException>(
				sut.ToJSRuntime().InvokeVoidAsync("foo", "bar", 41).AsTask()
			);

			planned.Invocations.Count.ShouldBe(1);
			var invocation = planned.Invocations[0];
			invocation.Identifier.ShouldBe("foo");
			invocation.Arguments[0].ShouldBe("bar");
			invocation.Arguments[1].ShouldBe(42);
		}

		[Fact(DisplayName = "Argument matcher used in SetupVoid are matched with invocations")]
		public async Task Test014()
		{
			var sut = new MockJSRuntimeInvokeHandler(JSRuntimeMockMode.Strict);
			var planned = sut.SetupVoid("foo", args => args.Count == 2);

			var i1 = sut.ToJSRuntime().InvokeVoidAsync("foo", "bar", 42);

			await Should.ThrowAsync<UnplannedJSInvocationException>(
				sut.ToJSRuntime().InvokeVoidAsync("foo", 42).AsTask()
			);

			await Should.ThrowAsync<UnplannedJSInvocationException>(
				sut.ToJSRuntime().InvokeVoidAsync("foo").AsTask()
			 );

			planned.Invocations.Count.ShouldBe(1);
			var invocation = planned.Invocations[0];
			invocation.Identifier.ShouldBe("foo");
			invocation.Arguments.Count.ShouldBe(2);
			invocation.Arguments[0].ShouldBe("bar");
			invocation.Arguments[1].ShouldBe(42);
		}

		[Fact(DisplayName = "Empty Setup returns the same result for all matching return type invocation")]
		public async Task Test015()
		{
			var sut = new MockJSRuntimeInvokeHandler(JSRuntimeMockMode.Strict);
			var plannedInvoke = sut.Setup<Guid>();
			var jsRuntime = sut.ToJSRuntime();

			var expectedResult1 = Guid.NewGuid();
			plannedInvoke.SetResult(expectedResult1);
			var i1 = jsRuntime.InvokeAsync<Guid>("someFunc");

			var i2 = jsRuntime.InvokeAsync<Guid>("otherFunc");

			(await i1).ShouldBe(expectedResult1);
			(await i2).ShouldBe(expectedResult1);
		}

		[Fact(DisplayName = "Empty Setup only matches the configured return type")]
		public void Test016()
		{
			var sut = new MockJSRuntimeInvokeHandler(JSRuntimeMockMode.Strict);
			var planned = sut.Setup<Guid>();

			Should.Throw<UnplannedJSInvocationException>(() => { var _ = sut.ToJSRuntime().InvokeAsync<string>("foo"); });

			planned.Invocations.Count.ShouldBe(0);
		}

		[Fact(DisplayName = "Empty Setup allows to return different results by return types")]
		public async Task Test017()
		{
			var sut = new MockJSRuntimeInvokeHandler(JSRuntimeMockMode.Strict);
			var plannedInvoke1 = sut.Setup<Guid>();
			var plannedInvoke2 = sut.Setup<string>();
			var jsRuntime = sut.ToJSRuntime();

			var expectedResult1 = Guid.NewGuid();
			plannedInvoke1.SetResult(expectedResult1);
			var i1 = jsRuntime.InvokeAsync<Guid>("someFunc");

			var expectedResult2 = "somestring";
			plannedInvoke2.SetResult(expectedResult2);
			var i2 = jsRuntime.InvokeAsync<string>("otherFunc");

			(await i1).ShouldBe(expectedResult1);
			(await i2).ShouldBe(expectedResult2);
		}

		[Fact(DisplayName = "Empty Setup is only used when there is no handler exist for the invocation identifier")]
		public async Task Test018()
		{
			var sut = new MockJSRuntimeInvokeHandler(JSRuntimeMockMode.Strict);
			var catchAllplannedInvoke = sut.Setup<Guid>();
			var jsRuntime = sut.ToJSRuntime();

			var catchAllexpectedResult = Guid.NewGuid();
			catchAllplannedInvoke.SetResult(catchAllexpectedResult);

			var expectedResult = Guid.NewGuid();
			var plannedInvoke = sut.Setup<Guid>("func");
			plannedInvoke.SetResult(expectedResult);

			var i1 = jsRuntime.InvokeAsync<Guid>("someFunc");

			var i2 = jsRuntime.InvokeAsync<Guid>("func");

			(await i1).ShouldBe(catchAllexpectedResult);
			(await i2).ShouldBe(expectedResult);
		}

		[Fact(DisplayName = "Empty Setup uses the last set result")]
		public async Task Test019()
		{
			var sut = new MockJSRuntimeInvokeHandler(JSRuntimeMockMode.Strict);
			var plannedInvoke1 = sut.Setup<Guid>();
			var plannedInvoke2 = sut.Setup<Guid>();
			var jsRuntime = sut.ToJSRuntime();

			var expectedResult1 = Guid.NewGuid();
			var expectedResult2 = Guid.NewGuid();

			plannedInvoke1.SetResult(expectedResult1);
			plannedInvoke2.SetResult(expectedResult2);

			var i1 = jsRuntime.InvokeAsync<Guid>("someFunc");

			(await i1).ShouldBe(expectedResult2);
		}

		[Fact(DisplayName = "SetupVoid matches all void invocations")]
		public async Task Test020()
		{
			var identifier = "someFunc";
			var sut = new MockJSRuntimeInvokeHandler(JSRuntimeMockMode.Strict);
			var plannedInvoke = sut.SetupVoid();

			Should.Throw<UnplannedJSInvocationException>(() => { var _ = sut.ToJSRuntime().InvokeAsync<string>(identifier); });

			var invocation = sut.ToJSRuntime().InvokeVoidAsync(identifier);
			plannedInvoke.SetVoid();

			await invocation;

			invocation.IsCompletedSuccessfully.ShouldBeTrue();
			plannedInvoke.Invocations.Count.ShouldBe(1);
		}

		[Fact(DisplayName = "Empty Setup is not used for invocation with void return types")]
		public async Task Test021()
		{
			var sut = new MockJSRuntimeInvokeHandler(JSRuntimeMockMode.Strict);
			var plannedInvoke = sut.Setup<Guid>();

			await Should.ThrowAsync<UnplannedJSInvocationException>(sut.ToJSRuntime().InvokeVoidAsync("someFunc").AsTask());
		}

		[Fact(DisplayName = "SetupVoid is only used when there is no void handler")]
		public async Task Test022()
		{
			var identifier = "someFunc";
			var sut = new MockJSRuntimeInvokeHandler(JSRuntimeMockMode.Strict);
			var plannedInvoke = sut.SetupVoid(identifier);
			var plannedCatchall = sut.SetupVoid();

			var invocation = sut.ToJSRuntime().InvokeVoidAsync(identifier);
			plannedInvoke.SetVoidResult();

			await invocation;

			invocation.IsCompletedSuccessfully.ShouldBeTrue();
			plannedInvoke.Invocations.Count.ShouldBe(1);
			plannedCatchall.Invocations.Count.ShouldBe(0);
		}

		[Fact(DisplayName = "Mock returns default value from IJSInProcessRuntime's invoke method in loose mode without invocation setup")]
		public void Test023()
		{
			var sut = new MockJSRuntimeInvokeHandler(JSRuntimeMockMode.Loose);

			var result = ((IJSInProcessRuntime)sut.ToJSRuntime()).Invoke<object>("ident", Array.Empty<object>());

			result.ShouldBe(default);
		}

		[Fact(DisplayName = "After IJSInProcessRuntime invocation a invocation should be visible from the Invocations list")]
		public void Test024()
		{
			var identifier = "fooFunc";
			var args = new[] { "bar", "baz" };
			using var cts = new CancellationTokenSource();
			var sut = new MockJSRuntimeInvokeHandler(JSRuntimeMockMode.Loose);

			var _ = ((IJSInProcessRuntime)sut.ToJSRuntime()).Invoke<object>(identifier, args);

			var invocation = sut.InProcessInvocations[identifier].Single();
			invocation.Identifier.ShouldBe(identifier);
			invocation.Arguments.ShouldBe(args);
		}

		[Fact(DisplayName = "IJSInProcessRuntime invocations receive the result set in a planned invocation")]
		public void Test025()
		{
			var identifier = "func";
			var sut = new MockJSRuntimeInvokeHandler(JSRuntimeMockMode.Strict);

			var expectedResult = Guid.NewGuid();
			sut.SetupInProcess<Guid>(identifier, expectedResult);

			var i = ((IJSInProcessRuntime)sut.ToJSRuntime()).Invoke<Guid>(identifier);

			i.ShouldBe(expectedResult);
		}

		[Fact(DisplayName = "Mock throws exception when in strict mode and IJSInProcessRuntime invocation has not been setup")]
		public void Test026()
		{
			var sut = new MockJSRuntimeInvokeHandler(JSRuntimeMockMode.Strict);
			var identifier = "func";
			var args = new[] { "bar", "baz" };

			var exception = Should.Throw<UnplannedIJSInProcessInvocationException>(() => { var _ = ((IJSInProcessRuntime)sut.ToJSRuntime()).Invoke<object>(identifier, args); });
			exception.Invocation.Identifier.ShouldBe(identifier);
			exception.Invocation.Arguments.ShouldBe(args);
		}
	}
}
