using System;
using System.Fabric.Query;
using System.Globalization;
using System.Reflection;

/// <summary>
/// mock <see cref="Application"/> object from Query Manager - and due to MS design, this must go via reflection
/// </summary>
public static class MockQueryApplicationFactory
{
    /// <summary>
    /// create mock instance - we only use <see cref="Application.ApplicationName"/> at this moment
    /// </summary>
    /// <param name="appName">desired app name</param>
    /// <returns>mocked instance</returns>
    public static Application CreateApplication(string appName)
    {
        var instance =  (Application)Activator.CreateInstance(typeof(Application), BindingFlags.Instance | BindingFlags.NonPublic, (Binder)null,null, CultureInfo.CurrentCulture);
        instance.GetType().GetProperty("ApplicationName").SetValue(instance, new Uri($"fabric:/{appName}"));

        return instance;
    }
}
