using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Rodnarok.Tests
{
    /// <summary>
    /// A release packages Rodnarok.dll alone, so a player installing Rodnarok gets nothing of the
    /// self-test: Rodnarok holds only the hook the self-test plugs into (<c>SelfTestHost</c>) and
    /// nothing else. The runner, its scenarios, its command and its files come with
    /// <c>Rodnarok.SelfTest.dll</c>, which only a development build installs, and installing it is
    /// what turns the self-test on, so Rodnarok has no setting for it.
    /// </summary>
    public class SelfTestContainmentTests
    {
        /// <summary>The one part of the self-test Rodnarok holds: where it plugs in, with what it offers.</summary>
        private static readonly string[] Hook = { "SelfTestHost", "IRunner" };

        [Fact]
        public void RodnarokAloneHoldsNothingOfTheSelfTestButTheHookItPlugsInto()
        {
            var found = Declared(RodnarokSource.Compilation)
                .Where(t => Names(t.Name) && !Hook.Contains(t.Name))
                .Select(t => $"{t.Where} {t.Name}");
            Violations.None("hold only the hook of the self-test in Rodnarok", found);
        }

        [Fact]
        public void RodnarokAloneNamesNoSelfTestSettingCommandOrFile()
        {
            var found = Literals(RodnarokSource.Compilation)
                .Where(l => l.Text.IndexOf("selftest", StringComparison.OrdinalIgnoreCase) >= 0)
                .Select(l => $"{l.Where} \"{l.Text}\"");
            Violations.None("name no self-test setting, command or file in Rodnarok", found);
        }

        [Fact]
        public void TheSelfTestPluginBringsItsRunnerAndItsCommand()
        {
            var types = Declared(RodnarokSource.SelfTest).Select(t => t.Name).ToList();
            Assert.Contains("ScenarioRunner", types);
            Assert.Contains("SelfTestCommand", types);
            Assert.Contains(Literals(RodnarokSource.SelfTest), l => l.Text.Equals("selftest", StringComparison.OrdinalIgnoreCase));
        }

        [Theory]
        [InlineData("ScenarioRunner")]
        [InlineData("SelfTestCommand")]
        [InlineData("SkipScenario")]
        public void APartOfTheSelfTestIsKnownByItsName(string name) => Assert.True(Names(name));

        [Theory]
        [InlineData("Rodnarok")]
        [InlineData("BaitTable")]
        [InlineData("Testing")]
        public void WhatIsNoPartOfTheSelfTestIsLeftAlone(string name) => Assert.False(Names(name));

        /// <summary>Whether a type's name says it is part of the self-test or its runs.</summary>
        private static bool Names(string name) =>
            name.IndexOf("SelfTest", StringComparison.Ordinal) >= 0 || name.IndexOf("Scenario", StringComparison.Ordinal) >= 0;

        /// <summary>Every type a compilation declares, nested ones too, with where.</summary>
        private static IEnumerable<(string Name, string Where)> Declared(CSharpCompilation compilation)
        {
            foreach (var tree in compilation.SyntaxTrees.Where(t => !string.IsNullOrEmpty(t.FilePath)))
            {
                foreach (var type in tree.GetRoot().DescendantNodes().OfType<BaseTypeDeclarationSyntax>()) yield return (type.Identifier.Text, RodnarokSource.Where(type));
            }
        }

        /// <summary>Every string a compilation writes, plain or as the text of an interpolated one, with where.</summary>
        private static IEnumerable<(string Text, string Where)> Literals(CSharpCompilation compilation)
        {
            foreach (var tree in compilation.SyntaxTrees.Where(t => !string.IsNullOrEmpty(t.FilePath)))
            {
                foreach (var node in tree.GetRoot().DescendantNodes())
                {
                    if (node is LiteralExpressionSyntax literal && literal.IsKind(SyntaxKind.StringLiteralExpression)) yield return (literal.Token.ValueText, RodnarokSource.Where(node));
                    else if (node is InterpolatedStringTextSyntax text) yield return (text.TextToken.ValueText, RodnarokSource.Where(node));
                }
            }
        }
    }
}
