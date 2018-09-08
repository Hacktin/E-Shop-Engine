﻿using System;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using AutoMapper;
using E_Shop_Engine.Domain.DomainModel.IdentityModel;
using E_Shop_Engine.Services.Data.Identity;
using E_Shop_Engine.Website.Models;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.Owin;
using Microsoft.Owin.Security;

namespace E_Shop_Engine.Website.Controllers
{
    public class AccountController : Controller
    {
        private IAuthenticationManager AuthManager
        {
            get
            {
                return HttpContext.GetOwinContext().Authentication;
            }
        }

        private AppUserManager UserManager
        {
            get
            {
                return HttpContext.GetOwinContext().GetUserManager<AppUserManager>();
            }
        }

        [Authorize]
        public async Task<ActionResult> Index()
        {
            string userId = HttpContext.User.Identity.GetUserId();
            AppUser user = await UserManager.FindByIdAsync(userId);
            UserEditViewModel model = Mapper.Map<UserEditViewModel>(user);

            return View(model);
        }

        [Authorize]
        public ActionResult ChangePassword()
        {
            return View(new UserChangePasswordViewModel());
        }

        [Authorize]
        [ValidateAntiForgeryToken]
        [HttpPost]
        public async Task<ActionResult> ChangePassword(UserChangePasswordViewModel model)
        {
            if (model.NewPassword != model.NewPasswordCopy)
            {
                ModelState.AddModelError("", "The new password and confirmation password does not match.");
                return View(model);
            }
            if (!ModelState.IsValid)
            {
                return View(model);
            }
            string userId = HttpContext.User.Identity.GetUserId();
            AppUser user = await UserManager.FindByIdAsync(userId);

            IdentityResult validPass = null;
            validPass = await UserManager.PasswordValidator.ValidateAsync(model.NewPassword);
            if (validPass.Succeeded)
            {
                user.PasswordHash = UserManager.PasswordHasher.HashPassword(model.NewPassword);
            }
            else
            {
                AddErrorsFromResult(validPass);
            }

            if (validPass == null || (model.NewPassword != string.Empty && validPass.Succeeded))
            {
                IdentityResult result = await UserManager.UpdateAsync(user);
                if (result.Succeeded)
                {
                    return RedirectToAction("Index");
                }
                else
                {
                    AddErrorsFromResult(result);
                }
            }
            else
            {
                ModelState.AddModelError("", "User Not Found");
            }
            return View(model);
        }

        [Authorize]
        public async Task<ActionResult> Edit()
        {
            string userId = HttpContext.User.Identity.GetUserId();
            AppUser user = await UserManager.FindByIdAsync(userId);
            UserEditViewModel model = Mapper.Map<UserEditViewModel>(user);

            if (user != null)
            {
                return View(model);
            }
            else
            {
                return RedirectToAction("Index");
            }
        }
        //TODO validation returnurl
        [Authorize]
        [ValidateAntiForgeryToken]
        [HttpPost]
        public async Task<ActionResult> Edit(UserEditViewModel model)
        {
            string userId = HttpContext.User.Identity.GetUserId();
            AppUser user = await UserManager.FindByIdAsync(userId);
            if (user != null)
            {
                user.Email = model.Email;
                IdentityResult validEmail = await UserManager.UserValidator.ValidateAsync(user);
                if (!validEmail.Succeeded)
                {
                    AddErrorsFromResult(validEmail);
                }

                if (validEmail.Succeeded)
                {
                    user.Name = model.Name;
                    user.Surname = model.Surname;
                    user.PhoneNumber = model.PhoneNumber;
                    user.UserName = model.Email;
                    IdentityResult result = await UserManager.UpdateAsync(user);
                    if (result.Succeeded)
                    {
                        return RedirectToAction("Index");
                    }
                    else
                    {
                        AddErrorsFromResult(result);
                    }
                }
            }
            else
            {
                ModelState.AddModelError("", "User Not Found");
            }
            return View(model);
        }

        [AllowAnonymous]
        public ActionResult Login(string returnUrl)
        {
            if (HttpContext.User.Identity.IsAuthenticated)
            {
                return View("_Error", new string[] { "Access denied" });
            }
            ViewBag.returnUrl = returnUrl;
            return View();
        }

        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        [HttpPost]
        public async Task<ActionResult> Login(UserLoginViewModel model, string returnUrl)
        {
            if (ModelState.IsValid)
            {
                AppUser user = await UserManager.FindAsync(model.Email, model.Password);
                if (user == null)
                {
                    ModelState.AddModelError("", "Invalid name or password");
                }
                else
                {
                    ClaimsIdentity ident = await UserManager.CreateIdentityAsync(user, DefaultAuthenticationTypes.ApplicationCookie);
                    AuthManager.SignOut();
                    AuthManager.SignIn(new AuthenticationProperties
                    {
                        IsPersistent = false
                    }, ident);
                    return Redirect(returnUrl);
                }
            }
            ViewBag.returnUrl = returnUrl;
            return View(model);
        }

        [Authorize]
        public ActionResult Logout()
        {
            AuthManager.SignOut();
            return RedirectToAction("Index", "Home");
        }

        [AllowAnonymous]
        public ActionResult Create()
        {
            return View();
        }

        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        [HttpPost]
        public async Task<ActionResult> Create(UserCreateViewModel model)
        {
            if (ModelState.IsValid)
            {
                AppUser user = Mapper.Map<AppUser>(model);
                user.Created = DateTime.UtcNow;

                IdentityResult result = await UserManager.CreateAsync(user, model.Password);
                if (result.Succeeded)
                {
                    return RedirectToAction("Index", "Home");
                }
                else
                {
                    AddErrorsFromResult(result);
                }
            }

            return View(model);
        }

        private void AddErrorsFromResult(IdentityResult result)
        {
            foreach (string error in result.Errors)
            {
                ModelState.AddModelError("", error);
            }
        }
    }
}