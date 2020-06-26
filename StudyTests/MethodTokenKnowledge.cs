using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NUnit.Framework;

namespace StudyTests
{
    [TestFixture]
    public class MethodTokenKnowledge
    {
        private const string CodeExample = @"
using System;
namespace Test
{
    public class BaseTestClass
    {
        public virtual void BasedMethod(){} 
    }
    public class TestClass: BaseTestClass
    {
        public int Method1(int a, int b) => a+b;
        public int Method2(int a,int b) => a-b;
        public static void Method3() => Console.WriteLine(""Test"");
        public override void BasedMethod(){}
    }
}";

        [Test]
        public void MetadataTokensTwoIdenticalAssembliesEqual()
        {
            var firstAssembly = CreateAssemblyWithName("test1");
            var secondAssembly = CreateAssemblyWithName("test2");

            var firstAssemblyMethods = GetAllMethods(firstAssembly);
            var secondAssemblyMethods = GetAllMethods(secondAssembly);

            Assert.AreEqual(
                firstAssemblyMethods.Select(x => x.Value.MetadataToken),
                secondAssemblyMethods.Select(x => x.Value.MetadataToken));
        }

        private static KeyValuePair<string, MethodInfo>[] GetAllMethods(Assembly secondAssembly,
            string testclass = "TestClass")
        {
            return secondAssembly
                .GetTypes()
                .First(x => x.Name == testclass).GetMethods()
                .ToDictionary(x => x.Name, x => x)
                .ToArray();
        }

        [Test]
        public void OverridedMethodHasDifferentMetadataTokenThanBased()
        {
            var assembly = CreateAssemblyWithName("test1");

            var basedMethod = GetAllMethods(assembly, "BaseTestClass")
                .First(x => x.Key == "BasedMethod")
                .Value
                .MetadataToken;

            var overiddenMethod = GetAllMethods(assembly)
                .First(x => x.Key == "BasedMethod")
                .Value
                .MetadataToken;

            Assert.AreNotEqual(basedMethod, overiddenMethod);
        }


        private Assembly CreateAssemblyWithName(string parameter)
        {
            var metadata = new[]
            {
                MetadataReference.CreateFromFile(
                    AppDomain.CurrentDomain
                        .GetAssemblies()
                        .First(x => x.FullName.ToLower().Contains("mscorlib"))
                        .Location
                )
            };

            var first = CSharpCompilation.Create(parameter)
                .WithOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
                .WithReferences(metadata)
                .AddSyntaxTrees(SyntaxFactory.ParseSyntaxTree(CodeExample));

            var pe = new MemoryStream();
            var pdb = new MemoryStream();
            var result = first.Emit(pe, pdb);
            var firstAssembly = Assembly.Load(pe.ToArray(), pdb.ToArray());
            return firstAssembly;
        }
    }
}