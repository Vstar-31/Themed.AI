using System;
using System.Reflection;
using Microsoft.Web.WebView2.Core;

class Program {
    static void Main() {
        var type = typeof(CoreWebView2Environment);
        foreach (var method in type.GetMethods()) {
            if (method.Name.StartsWith("Create")) {
                Console.Write(method.Name + "(");
                var pars = method.GetParameters();
                for (int i = 0; i < pars.Length; i++) {
                    Console.Write(pars[i].ParameterType.Name + " " + pars[i].Name);
                    if (i < pars.Length - 1) Console.Write(", ");
                }
                Console.WriteLine(")");
            }
        }
    }
}
