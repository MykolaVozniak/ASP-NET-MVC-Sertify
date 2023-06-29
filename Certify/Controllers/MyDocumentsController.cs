﻿using Certify.Data;
using Certify.Models;
using Certify.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http.Extensions;
using Newtonsoft.Json;
using AutoMapper;

namespace Certify.Controllers
{
    public class MyDocumentsController : Controller
    {
        private readonly CertifyDbContext _context;
        private readonly IWebHostEnvironment _appEnvironment;
        private readonly UserManager<User> _userManager;
        private readonly IMapper _mapper;

        public MyDocumentsController(CertifyDbContext context, IWebHostEnvironment appEnvironment, UserManager<User> userManager, IMapper mapper)
        {
            _context = context;
            _appEnvironment = appEnvironment;
            _userManager = userManager;
            _mapper = mapper;
        }

        //Index
        [Authorize]
        public IActionResult Index()
        {
            var documents = _context.Documents.ToList();
            return View("Index", documents);
        }

        //Create
        [Authorize]
        public async Task<IActionResult> CreateAsync()
        {
            return View("Create");
        }

        private async Task<List<string>> GetEmailListAsync()
        {
            var user = await _userManager.GetUserAsync(HttpContext.User);
            string userId = user.Id;

            var emailList = _context.Users
                .Where(u => u.Id != userId)
                .Select(u => u.Email)
                .ToList();

            return emailList;
        }
        public async Task<JsonResult> GetEmailList()
        {
            var emailList = await GetEmailListAsync();
            return Json(emailList);
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> AddFile(DocumentCreate dasc)
        {

            if (!ModelState.IsValid)
            {
                return View("Create", dasc);
            }

            var user = await _userManager.GetUserAsync(HttpContext.User);
            string path = "/Documents/" + dasc.UploadedFile.FileName;

            using (var fileStream = new FileStream(_appEnvironment.WebRootPath + path, FileMode.Create))
            {
                await dasc.UploadedFile.CopyToAsync(fileStream);
            }

            var document = new Document
            {
                Title = dasc.Title,
                ShortDescription = dasc.ShortDescription,
                FileURL = path,
                UserId = user.Id,
                UploadedDate = DateTime.Now
            };
            _context.Documents.Add(document);
            await _context.SaveChangesAsync();

            var lastDocument = _context.Documents.OrderByDescending(d => d.Id).First();
            var signatures = new List<Signature>();
            var selectedEmails = JsonConvert.DeserializeObject<List<string>>(dasc.UserEmail);
            foreach (var userEmail in selectedEmails)
            {
                string userId = GetUserIdByEmail(userEmail);
                if (userId != null)
                {
                    var signature = new Signature
                    {
                        IsSigned = null,
                        DocumentId = lastDocument.Id,
                        UserId = userId
                    };

                    signatures.Add(signature);
                }
            }
            _context.Signatures.AddRange(signatures);

            await _context.SaveChangesAsync();

            return RedirectToAction("Index");

        }

        [HttpGet]
        public async Task<IActionResult> CheckEmailExistsAsync(string email)
        {
            var currentUser = await _userManager.GetUserAsync(HttpContext.User);
            bool exists = _context.Users.Any(u => u.Email == email && u.Id != currentUser.Id);
            return Json(new { exists });
        }
        private string GetUserIdByEmail(string email)
        {
            var user = _context.Users.FirstOrDefault(u => u.Email == email);
            return user.Id;
        }

        //Info
        [HttpGet]
        public async Task<IActionResult> InfoAsync(int id)
        {
            DocumentViewModel document = _mapper.Map<Document, DocumentViewModel>(_context.Documents.Find(id));
            SelectUserSigned(document);

            ViewBag.IsUserSignatuer = await IsUserSignaturer(document);
            ViewBag.IsUserOwner = await IsUserOwner(document);


            if (document == null)
            {
                return NotFound();
            }
            else
            {
                ViewBag.ReturnUrl = Request.Headers["Referer"].ToString();
                ViewBag.CurrentUrl = HttpContext.Request.GetDisplayUrl().ToString();
                return View(document);
            }
        }


        private async Task<bool> IsUserSignaturer(DocumentViewModel document)
        {
            var currentUser = await _userManager.GetUserAsync(HttpContext.User);

            bool isUserSignatuer = _context.Signatures.Any(s => s.DocumentId == document.Id && s.UserId == currentUser.Id && s.IsSigned == null);

            return isUserSignatuer;
        }

        private async Task<bool> IsUserOwner(DocumentViewModel document)
        {
            var currentUser = await _userManager.GetUserAsync(HttpContext.User);

            bool isUserOwner = _context.Documents.Any(d => d.Id == document.Id && d.UserId == currentUser.Id);

            return isUserOwner;
        }

        //Method exist User Signature
        private void SelectUserSigned(DocumentViewModel document)
        {
            var signedUsers = _context.Signatures
                    .Include(s => s.User)
                    .Where(s => s.DocumentId == document.Id)
                    .Select(s => new
                    {
                        IsSigned = s.IsSigned,
                        UserDescription = $"{s.User.Firstname} {s.User.Lastname} ({s.User.Email})"
                    })
                    .ToList();

            ViewBag.SignedTrue = signedUsers.Where(s => s.IsSigned == true)
                                            .Select(s => s.UserDescription)
                                            .ToList();
            ViewBag.SignedNull = signedUsers.Where(s => s.IsSigned == null)
                                            .Select(s => s.UserDescription)
                                            .ToList();
            ViewBag.SignedFalse = signedUsers.Where(s => s.IsSigned == false)
                                             .Select(s => s.UserDescription)
                                             .ToList();
        }
    }
}