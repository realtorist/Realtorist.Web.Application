using System;

namespace Realtorist.Web.Application.Attributes
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class RequireAuthorizationAttribute : Attribute
    {
        
    }
}