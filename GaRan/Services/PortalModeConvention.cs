using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace GaRan.Services;

/// <summary>Limits each launched portal to the controllers and actions that belong to it.</summary>
public sealed class PortalModeConvention(string mode) : IApplicationModelConvention
{
    private static readonly HashSet<string> InternalAccountActions =
        ["StaffLogin", "AdminLogin", "EmployeeLogin", "AccessDenied", "Logout"];

    public void Apply(ApplicationModel application)
    {
        var internalPortal = mode.Equals("Internal", StringComparison.OrdinalIgnoreCase);

        foreach (var controller in application.Controllers.ToArray())
        {
            var controllerName = controller.ControllerName;
            var isAdminArea = controller.RouteValues.TryGetValue("area", out var area)
                && string.Equals(area, "Admin", StringComparison.OrdinalIgnoreCase);

            if (internalPortal)
            {
                if (!isAdminArea && controllerName is not ("Account" or "Home" or "Payment"))
                {
                    application.Controllers.Remove(controller);
                    continue;
                }

                if (controllerName == "Home")
                {
                    foreach (var action in controller.Actions.Where(x => x.ActionName != "Error").ToArray())
                        controller.Actions.Remove(action);
                }
                else if (controllerName == "Account")
                {
                    foreach (var action in controller.Actions.Where(x => !InternalAccountActions.Contains(x.ActionName)).ToArray())
                        controller.Actions.Remove(action);
                }
                else if (controllerName == "Payment")
                {
                    foreach (var action in controller.Actions.Where(x => x.ActionName != "Confirm").ToArray())
                        controller.Actions.Remove(action);
                }
            }
            else
            {
                if (isAdminArea)
                {
                    application.Controllers.Remove(controller);
                    continue;
                }

                if (controllerName == "Account")
                {
                    foreach (var action in controller.Actions.Where(x => x.ActionName is "StaffLogin" or "EmployeeLogin").ToArray())
                        controller.Actions.Remove(action);
                }
            }
        }
    }
}
