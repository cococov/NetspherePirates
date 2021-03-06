﻿using System;
using Netsphere.Tools.ShopEditor.Models;
using Netsphere.Tools.ShopEditor.ViewModels.Controls;

namespace Netsphere.Tools.ShopEditor.Views.Controls
{
    public sealed class EffectGroup : View<EffectGroupViewModel>
    {
        public EffectGroup()
        {
            InitializeComponent();
        }

        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);
            if (DataContext is ShopEffectGroup effectGroup)
                DataContext = ViewModel = new EffectGroupViewModel(effectGroup);
        }
    }
}
