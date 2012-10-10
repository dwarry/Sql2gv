using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;

using Caliburn.Micro;

namespace Sql2gv.Ui
{
    public class Bootstrap : Caliburn.Micro.Bootstrapper<ShellViewModel>
    {
        protected override void Configure()
        {
            base.Configure();

            OverrideBindingConventions();
        }

        protected override void OnExit(object sender, EventArgs e)
        {
            base.OnExit(sender, e);


        }

        private void OverrideBindingConventions()
        {
            Action<DependencyProperty, DependencyObject, Binding, PropertyInfo> oldApplyUpdateSourceTrigger =
                    ConventionManager.ApplyUpdateSourceTrigger;

            ConventionManager.ApplyUpdateSourceTrigger = (bindableProperty, element, binding, info) =>
            {
                if (info.Name == "SqlInstance" && info.ReflectedType == typeof(ShellViewModel))
                {
                    binding.UpdateSourceTrigger = UpdateSourceTrigger.LostFocus;
                    return;
                }

                oldApplyUpdateSourceTrigger(bindableProperty,
                                            element,
                                            binding,
                                            info);
            };            
        }
    }
}
