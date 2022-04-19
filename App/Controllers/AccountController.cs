﻿#nullable disable

using App.Models.DatabaseModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace App.Controllers;

public class AccountController : Controller
{
    private readonly UserManager<Driver> _userManager;
    private readonly SignInManager<Driver> _signInManager;
    private readonly EmailService.ISender _emailSender;

    public AccountController(UserManager<Driver> userManager, SignInManager<Driver> signInManager, EmailService.ISender sender)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _emailSender = sender;
    }

    [HttpGet]
    public IActionResult Index()
    {
        return View();
    }

    #region Register
    [HttpGet]
    public IActionResult Register()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(Models.ViewModels.Account.RegistrationModel userModel)
    {
        if (!ModelState.IsValid)
        {
            return View(userModel);
        }

        Driver user = new Driver();
        user.FirstName = userModel.FirstName;
        user.LastName = userModel.LastName;
        user.BirthDate = userModel.BirthDate;
        user.UserName = userModel.Email;
        user.Email = userModel.Email;

        IdentityResult result = await _userManager.CreateAsync(user, userModel.Password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.TryAddModelError(error.Code, error.Description);
            }
            return View(userModel);
        }

        string token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
        string link = Url.Action(nameof(ConfirmEmail), "Account", new { token, email = user.Email }, Request.Scheme);
        string text = string.Format("Click here to confirm your email: {0}", link);

        EmailService.Message message = new EmailService.Message(new string[] { user.Email }, "EpicRaceEvents - Account confirmation", text, null);
        await _emailSender.SendEmailAsync(message);

        return RedirectToAction(nameof(HomeController.Index), "Home");
    }

    [HttpGet]
    public async Task<IActionResult> ConfirmEmail(string token, string email)
    {
        Driver user = await _userManager.FindByEmailAsync(email);
        if (user == null)
            return View("Error");

        IdentityResult result = await _userManager.ConfirmEmailAsync(user, token);
        if (result.Succeeded)
        {
            await _signInManager.SignInAsync(user, false);
            return View(nameof(ConfirmEmail));
        }
        return View("Error");
    }
    #endregion

    #region Login
    [HttpGet]
    public IActionResult Login(string returnUrl)
    {
        ViewData["ReturnUrl"] = returnUrl;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(Models.ViewModels.Account.LoginModel userModel, string returnUrl)
    {
        if (!ModelState.IsValid)
        {
            return View(userModel);
        }

        var result = await _signInManager.PasswordSignInAsync(userModel.Email, userModel.Password, userModel.RememberMe, lockoutOnFailure: true);
        if (result.Succeeded)
        {
            return RedirectToLocal(returnUrl);
        }
        else if (result.IsLockedOut)
        {
            string forgotPassLink = Url.Action(nameof(ForgotPassword), "Account", new { }, Request.Scheme);
            string content = string.Format("Your account is locked out, to reset your password, please click this link : {0}", forgotPassLink);

            EmailService.Message message = new EmailService.Message(new string[] { userModel.Email }, "EpicRaceEvents - Account locked", content, null);
            await _emailSender.SendEmailAsync(message);

            ModelState.AddModelError("", "The account is locked out ! Check your email to reset your password.");
            return View();
        }
        else
        {
            ModelState.AddModelError("", "Invalid UserName or Password");
            return View();
        }
    }
    #endregion

    #region Forgot/Reset Password
    [HttpGet]
    public IActionResult ForgotPassword()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(Models.ViewModels.Account.ForgotPasswordModel forgotPasswordModel)
    {
        if (!ModelState.IsValid)
            return View(forgotPasswordModel);

        Driver user = await _userManager.FindByEmailAsync(forgotPasswordModel.Email);
        if (user == null)
            return RedirectToAction(nameof(ForgotPasswordConfirmation));

        string token = await _userManager.GeneratePasswordResetTokenAsync(user);
        string callback = Url.Action(nameof(ResetPassword), "Account", new { token, email = user.Email }, Request.Scheme);
        string text = string.Format("Click here to reset your password : {0}", callback);
        EmailService.Message message = new EmailService.Message(new string[] { user.Email }, "EpicRaceEvents - Reset password", text, null);

        await _emailSender.SendEmailAsync(message);
        return RedirectToAction(nameof(ForgotPasswordConfirmation));
    }

    public IActionResult ForgotPasswordConfirmation()
    {
        return View();
    }

    [HttpGet]
    public IActionResult ResetPassword(string token, string email)
    {
        var model = new Models.ViewModels.Account.ResetPasswordModel { Token = token, Email = email };
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(Models.ViewModels.Account.ResetPasswordModel resetPasswordModel)
    {
        if (!ModelState.IsValid)
            return View(resetPasswordModel);

        Driver user = await _userManager.FindByEmailAsync(resetPasswordModel.Email);
        if (user == null)
            RedirectToAction(nameof(ResetPasswordConfirmation));

        IdentityResult resetPassResult = await _userManager.ResetPasswordAsync(user, resetPasswordModel.Token, resetPasswordModel.Password);
        if (!resetPassResult.Succeeded)
        {
            foreach (var error in resetPassResult.Errors)
            {
                ModelState.TryAddModelError(error.Code, error.Description);
            }
            return View();
        }
        return RedirectToAction(nameof(ResetPasswordConfirmation));
    }

    [HttpGet]
    public IActionResult ResetPasswordConfirmation()
    {
        return View();
    }
    #endregion

    #region Logout
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction(nameof(HomeController.Index), "Home");
    }
    #endregion

    private IActionResult RedirectToLocal(string returnUrl)
    {
        if (Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);
        else
            return RedirectToAction(nameof(HomeController.Index), "Home");
    }
}
