using System.Reflection;
using FlickFest.Core;

namespace FlickFest.Tests.EditMode
{
    /// <summary>
    /// Test-only utility for poking at <see cref="GameModeDefinition"/>'s private
    /// serialized fields. Production code goes through the public properties;
    /// tests need to construct configurations without authoring assets.
    /// </summary>
    internal static class ModeHelpers
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;

        public static void SetField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(name, Flags);
            Assert.That(field != null, $"Field {name} not found on {target.GetType().Name}");
            field.SetValue(target, value);
        }

        // Tiny shim — NUnit's Assert pulled in only to keep this file dependency-free
        // outside of tests would force a Unity Engine dep; instead, throw.
        private static class Assert
        {
            public static void That(bool condition, string message)
            {
                if (!condition)
                {
                    throw new System.InvalidOperationException(message);
                }
            }
        }
    }
}
