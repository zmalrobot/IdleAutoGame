using System.Reflection;
using Xunit;
using Xunit.Abstractions;

namespace IdleAutoGame.Tests.Unit;

public class LlamaConfigInspectionTest
{
    private readonly ITestOutputHelper _output;

    public LlamaConfigInspectionTest(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void InspectNativeLibraryConfig()
    {
        var type = typeof(LLama.Common.ModelParams).Assembly.GetType("LLama.Native.NativeLibraryConfig");
        if (type != null)
        {
            _output.WriteLine($"Type: {type.FullName}");
            foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance))
            {
                _output.WriteLine($"Prop: {prop.PropertyType.Name} {prop.Name}");
            }
            foreach (var m in type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance))
            {
                _output.WriteLine($"Method: {m.ReturnType.Name} {m.Name}({string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name))})");
            }
        }
        else
        {
            _output.WriteLine("NativeLibraryConfig not found");
        }
    }
}

