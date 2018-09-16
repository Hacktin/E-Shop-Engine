﻿using System;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Web.Mvc;
using AutoMapper;
using E_Shop_Engine.Domain.DomainModel;
using E_Shop_Engine.Domain.DomainModel.IdentityModel;
using E_Shop_Engine.Domain.Interfaces;
using E_Shop_Engine.Services.Data.Identity;
using E_Shop_Engine.Website.Models;
using Microsoft.AspNet.Identity;
using Microsoft.Owin.Security;

namespace E_Shop_Engine.Website.Controllers
{
    public class AccountController : Controller
    {
        //private IAuthenticationManager AuthManager
        //{
        //    get
        //    {
        //        return HttpContext.GetOwinContext().Authentication;
        //    }
        //}

        //private AppUserManager UserManager
        //{
        //    get
        //    {
        //        return HttpContext.GetOwinContext().GetUserManager<AppUserManager>();
        //    }
        //}

        private readonly AppUserManager UserManager;
        private readonly IAuthenticationManager AuthManager;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IRepository<Address> _addressRepository;

        public AccountController(AppUserManager userManager, IAuthenticationManager authManager, IUnitOfWork unitOfWork, IRepository<Address> addressRepository)
        {
            UserManager = userManager;
            AuthManager = authManager;
            _unitOfWork = unitOfWork;
            _addressRepository = addressRepository;
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
            if (!ModelState.IsValid)
            {
                return View(model);
            }
            if (model.NewPassword != model.NewPasswordCopy)
            {
                ModelState.AddModelError("", "The new password and confirmation password does not match.");
                return View(model);
            }

            string userId = HttpContext.User.Identity.GetUserId();
            AppUser user = await UserManager.FindByIdAsync(userId);

            bool correctPass = await UserManager.CheckPasswordAsync(user, model.OldPassword);
            if (!correctPass)
            {
                ModelState.AddModelError("", "Please enter valid current password.");
                return View(model);
            }

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

        [Authorize]
        [ValidateAntiForgeryToken]
        [HttpPost]
        public async Task<ActionResult> Edit(UserEditViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }
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
        public ActionResult Login()
        {
            if (HttpContext.User.Identity.IsAuthenticated)
            {
                return View("_Error", new string[] { "You are already logged in." });
            }

            return View();
        }

        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        [HttpPost]
        public async Task<ActionResult> Login(UserLoginViewModel model)
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
                    return Redirect(ViewBag.returnUrl);
                }
            }

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
            if (HttpContext.User.Identity.IsAuthenticated)
            {
                return View("_Error", new string[] { "You are already logged in." });
            }

            return View();
        }

        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        [HttpPost]
        public async Task<ActionResult> Create(UserCreateViewModel model)
        {
            if (HttpContext.User.Identity.IsAuthenticated)
            {
                return Redirect(ViewBag.returnUrl);
            }

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

        public ActionResult Address()
        {
            string userId = HttpContext.User.Identity.GetUserId();
            AppUser user = UserManager.FindById(userId);

            AddressViewModel model;

            if (user.Address != null)
            {
                model = Mapper.Map<AddressViewModel>(user.Address);
            }
            else
            {
                model = new AddressViewModel();
            }

            return View(model);
        }

        [ValidateAntiForgeryToken]
        [HttpPost]
        public ActionResult Address(AddressViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return PartialView(model);
            }

            using (_unitOfWork.NewUnitOfWork())
            {
                string userId = HttpContext.User.Identity.GetUserId();
                AppUser user = UserManager.FindById(userId);
                Address address = _addressRepository.GetById(model.Id);
                address.City = model.City;
                address.Country = model.Country;
                address.Line1 = model.Line1;
                address.Line2 = model.Line2;
                address.State = model.State;
                address.Street = model.Street;
                address.ZipCode = model.ZipCode;
                _addressRepository.Update(address);
            }

            return RedirectToAction("Create", "Order", null);
        }

        public ActionResult AddressDetails()
        {
            string userId = HttpContext.User.Identity.GetUserId();
            AppUser user = UserManager.FindById(userId);

            AddressViewModel model;

            if (user.Address != null)
            {
                model = Mapper.Map<AddressViewModel>(user.Address);
            }
            else
            {
                return RedirectToAction("Address");
            }

            return PartialView(model);
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