using Xunit;
using FluentAssertions;

namespace Tests;

public class ContaTests
{
    [Fact]
    public void Test1()
    {
        true.Should().BeTrue();
    }
}

public class SimpleMath{ [Fact] public void Dummy() => 1.Should().Be(1); }
