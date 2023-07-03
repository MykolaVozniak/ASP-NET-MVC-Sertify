﻿using AutoMapper;
using Certify.ViewModels;
using Data;
using Data.Entity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

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

        //----------------------------------------------Index----------------------------------------------
        [Authorize]
        public async Task<IActionResult> IndexAsync()
        {
            var user = await _userManager.GetUserAsync(HttpContext.User);

            var documents = _context.Documents.Where(d => d.UserId == user.Id).ToList();
            List<ForMyDocumentsIndex> documentList = _mapper.Map<List<Data.Entity.Document>, List<ForMyDocumentsIndex>>(documents);

            foreach (var document in documentList)
            {
                int currentSignaturesCount = _context.Signatures.Count(s => s.DocumentId == document.Id && s.IsSigned != null);
                int maxSignaturesCount = _context.Signatures.Count(s => s.DocumentId == document.Id);
                bool isFalseSignatureExist = _context.Signatures.Any(s => s.DocumentId == document.Id && s.IsSigned == false);

                document.CurrentSignaturesCount = currentSignaturesCount;
                document.MaxSignaturesCount = maxSignaturesCount;

                if (isFalseSignatureExist)
                {
                    document.IsSigned = false;
                }
                else if (currentSignaturesCount != maxSignaturesCount)
                {
                    document.IsSigned = null;
                }
                else
                {
                    document.IsSigned = true;
                }
            }

            return View("Index", documentList);
        }


        //----------------------------------------------Delete----------------------------------------------
        [Authorize]
        public async Task<IActionResult> DeleteAsync(int id)
        {
            var document = _context.Documents.Find(id);
            var user = await _userManager.GetUserAsync(HttpContext.User);

            if (document == null || !await IsUserOwner(id))
            {
                return NotFound();
            }

            var signatures = _context.Signatures.Where(s => s.DocumentId == document.Id);
            _context.Signatures.RemoveRange(signatures);

            string filePath = _appEnvironment.WebRootPath + document.FileURL;
            string folderPath = Path.GetDirectoryName(filePath);

            if (System.IO.File.Exists(filePath))
            {
                System.IO.File.Delete(filePath);

                if (Directory.Exists(folderPath) && !Directory.EnumerateFiles(folderPath).Any())
                {
                    Directory.Delete(folderPath);
                }
            }

            _context.Documents.Remove(document);
            _context.SaveChanges();

            return RedirectToAction(nameof(Index));

        }

        //----------------------------------------------ChangeStatus----------------------------------------------
        public async Task<IActionResult> ChangeStatusAsync(bool status, int id)
        {
            var user = await _userManager.GetUserAsync(HttpContext.User);
            var signature = _context.Signatures.FirstOrDefault(s => s.DocumentId == id && s.UserId == user.Id);

            bool rightUser = await IsUserSignaturer(id);

            if (signature != null && rightUser)
            {
                if (status)
                {
                    signature.IsSigned = true;
                }
                else if (!status)
                {
                    signature.IsSigned = false;
                }

                signature.SignedDate = DateTime.Now;
                _context.SaveChanges();
            }

            return RedirectToAction("Index", "Notifications");
        }

        //----------------------------------------------Create----------------------------------------------
        [Authorize]
        public async Task<IActionResult> CreateAsync()
        {
            return View("Create");
        }


            public async Task<JsonResult> GetEmailListEdit(string documentId )
            {
                int? currentDocumentId = int.Parse(documentId);
                var user = await _userManager.GetUserAsync(HttpContext.User);
                string userId = user.Id;
                Console.WriteLine("wtsdfsdfsdfadfffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff   ------------- " + documentId);
                var selectSign =_context.Signatures
                    .Where(s => s.DocumentId == 4)
                    .Select(s => s.UserId)
                    .ToList();

            var emailList = _context.Users
                    .Where(u => u.Id != userId && selectSign.Contains(u.Id))
                    .Select(u => u.Email)
                    .ToList();

            return Json(emailList);
        }

        public async Task<JsonResult> GetEmailListCreate()
        {
            var user = await _userManager.GetUserAsync(HttpContext.User);
            string userId = user.Id;

            var emailList = _context.Users
                .Where(u => u.Id != userId)
                .Select(u => u.Email)
                .ToList();
            return Json(emailList);
        }
        [Authorize]
        [HttpPost]
        public async Task<IActionResult> AddFile(ForMyDocumentsCreate dasc)
        {

            if (!ModelState.IsValid)
            {
                return View("Create", dasc);
            }
            var user = await _userManager.GetUserAsync(HttpContext.User);
            string time = DateTime.Now.ToString("yyyyMMddHHmmss");

            string filePath = "/Documents/" + $"/{user.Id}-{time}/" + dasc.UploadedFile.FileName;
            string folderPath = Path.Combine(_appEnvironment.WebRootPath, "Documents", $"{user.Id}-{time}");
            Directory.CreateDirectory(folderPath);

            using (var fileStream = new FileStream(_appEnvironment.WebRootPath + filePath, FileMode.Create))
            {
                await dasc.UploadedFile.CopyToAsync(fileStream);
            }

            Data.Entity.Document document = _mapper.Map<ForMyDocumentsCreate, Data.Entity.Document>(dasc);
            document.UploadedDate = DateTime.Now;
            document.FileURL = filePath;
            document.UserId = user.Id;

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

        //----------------------------------------------Info----------------------------------------------
        [HttpGet]
        public async Task<IActionResult> InfoAsync(int id)
        {
            Data.Entity.Document doc = await _context.Documents.Include(d => d.User).FirstAsync(d => d.Id == id);
            ForMyDocumentsInfo document = _mapper.Map<Data.Entity.Document, ForMyDocumentsInfo>(doc);
            SelectUserSigned(document);
            ViewBag.IsUserSignatuer = await IsUserSignaturer(document.Id);
            ViewBag.IsUserOwner = await IsUserOwner(document.Id);

            if (document == null)
            {
                return NotFound();
            }
            ViewBag.ReturnUrl = Request.Headers["Referer"].ToString();
            ViewBag.CurrentUrl = HttpContext.Request.GetDisplayUrl().ToString();
            return View(document);

        }

        //----------------------------------------------Edit----------------------------------------------
        [Authorize]
        public async Task<IActionResult> EditAsync(int id)
        {
            ForMyDocumentsEdit? document = _mapper.Map<Data.Entity.Document, ForMyDocumentsEdit>(_context.Documents.Find(id));

            if (document == null || !await IsUserOwner(document.Id))
            {
                return NotFound();
            }

            return View(document);
        }

        [Authorize]
        [HttpPost]
        public IActionResult Edit(int id, ForMyDocumentsEdit updatedDocument)
        {
            Data.Entity.Document? document = _context.Documents.Find(id);

            if (document == null )
            {
                return NotFound();
            }

            document.Title = updatedDocument.Title;
            document.ShortDescription = updatedDocument.ShortDescription;

            _context.Documents.Update(document);
            _context.SaveChanges();

            if (updatedDocument.UserEmail != null)
            {
                var lastDocument = _context.Documents.OrderByDescending(d => d.Id).First();
                var signatures = new List<Signature>();
                var selectedEmails = JsonConvert.DeserializeObject<List<string>>(updatedDocument.UserEmail);
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

                _context.SaveChanges();
            }

            return RedirectToAction("Info", new { id = document.Id });
        }

        private void SelectUserSigned(ForMyDocumentsInfo document)
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

        public async Task<bool> IsUserSignaturer(int documentId)
        {
            if (!User.Identity.IsAuthenticated)
            {
                return false;
            }

            User? currentUser = await _userManager.GetUserAsync(HttpContext.User);

            bool isUserSignatuer = _context.Signatures.Any(s => s.DocumentId == documentId && s.UserId == currentUser.Id && s.IsSigned == null);

            return isUserSignatuer;
        }

        public async Task<bool> IsUserOwner(int documentId)
        {
            if (!User.Identity.IsAuthenticated)
            {
                return false;
            }

            User? currentUser = await _userManager.GetUserAsync(HttpContext.User);

            bool isUserOwner = _context.Documents.Any(d => d.Id == documentId && d.UserId == currentUser.Id);

            return isUserOwner;
        }
    }
}