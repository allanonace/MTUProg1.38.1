﻿// Copyright M. Griffie <nexus@nexussays.com>
//
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Xamarin.Forms;
using MTUComm;
using Library;

namespace aclara_meters.util
{
   public class BasePage : ContentPage
   {
        public BasePage ()
        {
            PageLinker.CurrentPage = this;

            // Reset previous main action reference
            Singleton.Remove<Action>();
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            (BindingContext as IBaseViewModel)?.OnAppearing();
        }
        
        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            (BindingContext as IBaseViewModel)?.OnDisappearing();
        }
    }
}
