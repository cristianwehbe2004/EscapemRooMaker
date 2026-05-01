using EscapeRoom.TriggerEngine.Abstractions;
using EscapeRoom.TriggerEngine.BuiltIns;
using EscapeRoom.TriggerEngine.Registry;
using FluentAssertions;

namespace EscapeRoom.Tests.Unit.TriggerEngine.Registry;

public class RegistryTests
{
    #region Condition Registry Tests

    public class ConditionRegistryTests
    {
        private readonly ConditionRegistry _registry;

        public ConditionRegistryTests()
        {
            _registry = new ConditionRegistry(new ActionTypeConditionEvaluator());
        }

        [Fact]
        public void Get_ShouldReturnEvaluator()
        {
            var evaluator = _registry.Get("actionTypeEquals");

            evaluator.Should().NotBeNull();
            evaluator.Should().BeOfType<ActionTypeConditionEvaluator>();
        }

        [Fact]
        public void Get_ShouldBeCaseInsensitive()
        {
            var lower = _registry.Get("actiontypeequals");
            var upper = _registry.Get("ACTIONTYPEEQUALS");
            var mixed = _registry.Get("ActionTypeEquals");

            lower.Should().BeSameAs(upper);
            upper.Should().BeSameAs(mixed);
        }

        [Fact]
        public void Get_ShouldThrowForUnknownType()
        {
            Action act = () => _registry.Get("unknown");

            act.Should().Throw<InvalidOperationException>()
                .WithMessage("*unknown*");
        }

        [Theory]
        [InlineData("actionTypeEquals")]
        [InlineData("actiontypeequals")]
        [InlineData("ACTIONTYPEEQUALS")]
        [InlineData("ActionTypeEquals")]
        public void Get_ShouldResolveDifferentCases(string typeName)
        {
            var evaluator = _registry.Get(typeName);

            evaluator.Should().NotBeNull();
        }
    }

    #endregion

    #region Combinator Registry Tests

    public class CombinatorRegistryTests
    {
        private readonly CombinatorRegistry _registry;

        public CombinatorRegistryTests()
        {
            _registry = new CombinatorRegistry(
                new AllTrueCombinatorEvaluator(),
                new AnyTrueCombinatorEvaluator());
        }

        [Theory]
        [InlineData("allTrue")]
        [InlineData("alltrue")]
        [InlineData("ALLTRUE")]
        [InlineData("AllTrue")]
        public void Get_ShouldResolveAllTrue(string typeName)
        {
            var evaluator = _registry.Get(typeName);

            evaluator.Should().NotBeNull();
            evaluator.Should().BeOfType<AllTrueCombinatorEvaluator>();
        }

        [Theory]
        [InlineData("anyTrue")]
        [InlineData("anytrue")]
        [InlineData("ANYTRUE")]
        [InlineData("AnyTrue")]
        public void Get_ShouldResolveAnyTrue(string typeName)
        {
            var evaluator = _registry.Get(typeName);

            evaluator.Should().NotBeNull();
            evaluator.Should().BeOfType<AnyTrueCombinatorEvaluator>();
        }

        [Fact]
        public void Get_ShouldThrowForUnknownType()
        {
            Action act = () => _registry.Get("unknown");

            act.Should().Throw<InvalidOperationException>()
                .WithMessage("*unknown*");
        }

        [Fact]
        public void Get_ShouldReturnDifferentInstancesForDifferentTypes()
        {
            var allTrue = _registry.Get("allTrue");
            var anyTrue = _registry.Get("anyTrue");

            allTrue.Should().NotBeSameAs(anyTrue);
        }
    }

    #endregion

    #region Effect Registry Tests

    public class EffectRegistryTests
    {
        private readonly EffectRegistry _registry;

        public EffectRegistryTests()
        {
            _registry = new EffectRegistry(
                new EmitMessageEffectExecutor(),
                new SetStateValueEffectExecutor());
        }

        [Theory]
        [InlineData("emitMessage")]
        [InlineData("emitmessage")]
        [InlineData("EMITMESSAGE")]
        [InlineData("EmitMessage")]
        public void Get_ShouldResolveEmitMessage(string typeName)
        {
            var executor = _registry.Get(typeName);

            executor.Should().NotBeNull();
            executor.Should().BeOfType<EmitMessageEffectExecutor>();
        }

        [Theory]
        [InlineData("setStateValue")]
        [InlineData("setstatevalue")]
        [InlineData("SETSTATEVALUE")]
        [InlineData("SetStateValue")]
        public void Get_ShouldResolveSetStateValue(string typeName)
        {
            var executor = _registry.Get(typeName);

            executor.Should().NotBeNull();
            executor.Should().BeOfType<SetStateValueEffectExecutor>();
        }

        [Fact]
        public void Get_ShouldThrowForUnknownType()
        {
            Action act = () => _registry.Get("unknown");

            act.Should().Throw<InvalidOperationException>()
                .WithMessage("*unknown*");
        }

        [Fact]
        public void Get_ShouldReturnDifferentInstancesForDifferentTypes()
        {
            var emit = _registry.Get("emitMessage");
            var set = _registry.Get("setStateValue");

            emit.Should().NotBeSameAs(set);
        }
    }

    #endregion
}