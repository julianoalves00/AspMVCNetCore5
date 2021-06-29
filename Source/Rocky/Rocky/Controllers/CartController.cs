using Braintree;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Rocky_DataAccess;
using Rocky_DataAccess.Repository.IRepository;
using Rocky_Models;
using Rocky_Models.ViewModels;
using Rocky_Utility;
using Rocky_Utility.BrainTree;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace Rocky.Controllers
{
    [Authorize]
    public class CartController : Controller
    {
        private readonly IWebHostEnvironment _webHostEnviroment;
        private readonly IEmailSender _emailSender;
        private readonly IApplicationUserRepository _userRepo;
        private readonly IProductRepository _prodRepo; 
        private readonly IInquiryHeaderRepository _inqHRepo;
        private readonly IInquiryDetailRepository _inqDRepo;
        private readonly IOrderHeaderRepository _orderHRepo;
        private readonly IOrderDetailRepository _orderDRepo;
        private readonly IBrainTreeGate _brain;

        [BindProperty]
        public ProductUserVM ProductUserVM { get; set; }

        public CartController(IWebHostEnvironment webHostEnviroment, IEmailSender emailSender,
            IApplicationUserRepository userRepo, IProductRepository prodRepo, IInquiryHeaderRepository inqHRepo, IInquiryDetailRepository inqDRepo,
            IOrderHeaderRepository orderHRepo, IOrderDetailRepository orderDRepo, IBrainTreeGate brain)
        {


            _webHostEnviroment = webHostEnviroment;
            _emailSender = emailSender;
            _userRepo = userRepo;
            _prodRepo = prodRepo;
            _inqHRepo = inqHRepo;
            _inqDRepo = inqDRepo;
            _orderHRepo = orderHRepo;
            _orderDRepo = orderDRepo;
            _brain = brain;
        }

        public IActionResult Index()
        {
            List<ShoppingCart> shoppingCartList = new List<ShoppingCart>();
            if(HttpContext.Session.Get<IEnumerable<ShoppingCart>>(WC.SessionCart) != null &&
                HttpContext.Session.Get<IEnumerable<ShoppingCart>>(WC.SessionCart).Count() > 0)
            {
                // sessions exists
                shoppingCartList = HttpContext.Session.Get<List<ShoppingCart>>(WC.SessionCart);
            }

            List<int> prodinCart = shoppingCartList.Select(i => i.ProductId).ToList();
            IEnumerable<Product> prodListTemp = _prodRepo.GetAll(filter: u => prodinCart.Contains(u.Id));
            IList<Product> prodList = new List<Product>();

            foreach (var cartObj in shoppingCartList)
            {
                Product prodTemp = prodListTemp.FirstOrDefault(u => u.Id == cartObj.ProductId);
                if (prodTemp != null)
                {
                    prodTemp.TempSqFt = cartObj.SqFt;
                    prodList.Add(prodTemp);
                }
            }

            return View(prodList);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ActionName("Index")]
        public IActionResult IndexPost(IEnumerable<Product> prodList)
        {
            List<ShoppingCart> shoppingCartList = new List<ShoppingCart>();

            foreach (Product prod in prodList)
            {
                shoppingCartList.Add(new ShoppingCart { ProductId = prod.Id, SqFt = prod.TempSqFt });
            }

            HttpContext.Session.Set(WC.SessionCart, shoppingCartList);

            return RedirectToAction(nameof(Summary));
        }

        public IActionResult Summary()
        {
            ApplicationUser applicationUser;

            if(User.IsInRole(WC.AdminRole))
            {
                if(HttpContext.Session.Get<int>(WC.SessionInquiryId) != 0)
                {
                    // cart has been load using an inquiry
                    InquiryHeader inquiryHeader = _inqHRepo.FirstOrDefault(filter: u => u.Id == HttpContext.Session.Get<int>(WC.SessionInquiryId));
                    applicationUser = new ApplicationUser() 
                    { 
                        Email = inquiryHeader.Email,
                        FullName = inquiryHeader.FullName,
                        PhoneNumber = inquiryHeader.PhoneNumber
                    };
                }
                else
                {
                    applicationUser = new ApplicationUser();
                }

                var gateway = _brain.GetGateway();
                var clienteToken = gateway.ClientToken.Generate();
                ViewBag.ClientToken = clienteToken;
            }
            else
            {
                var claimsIdentity = (ClaimsIdentity)User.Identity;
                var claim = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier);
                //var userId = User.FindFirstValue(ClaimTypes.Name);

                applicationUser = _userRepo.FirstOrDefault(filter: u => u.Id == claim.Value);
            }

            List<ShoppingCart> shoppingCartList = new List<ShoppingCart>();
            if (HttpContext.Session.Get<IEnumerable<ShoppingCart>>(WC.SessionCart) != null &&
                HttpContext.Session.Get<IEnumerable<ShoppingCart>>(WC.SessionCart).Count() > 0)
            {
                // sessions exists
                shoppingCartList = HttpContext.Session.Get<List<ShoppingCart>>(WC.SessionCart);
            }

            List<int> prodinCart = shoppingCartList.Select(i => i.ProductId).ToList();
            IEnumerable<Product> prodList = _prodRepo.GetAll(filter: u => prodinCart.Contains(u.Id));

            ProductUserVM = new ProductUserVM() 
            { 
                ApplicationUser = applicationUser
            };

            foreach(var cartObj in shoppingCartList)
            {
                Product prodTemp = _prodRepo.FirstOrDefault(filter: u => u.Id == cartObj.ProductId);
                prodTemp.TempSqFt = cartObj.SqFt;
                ProductUserVM.ProductList.Add(prodTemp);
            }

            return View(ProductUserVM);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ActionName("Summary")]
        public async Task<IActionResult> SummaryPost(IFormCollection collection, ProductUserVM ProductUserVM)
        {
            var claimsIdentity = (ClaimsIdentity) User.Identity;
            var claim = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier);

            if (User.IsInRole(WC.AdminRole))
            {
                //we need to create an order

                OrderHeader orderHeader = new OrderHeader()
                {
                    CreatedByUserId = claim.Value,
                    FinalOrderTotal = ProductUserVM.ProductList.Sum(u => u.TempSqFt * u.Price),
                    City = ProductUserVM.ApplicationUser.City,
                    StreetAddress = ProductUserVM.ApplicationUser.StreetAddress,
                    State = ProductUserVM.ApplicationUser.State,
                    PostalCode = ProductUserVM.ApplicationUser.PostalCode,
                    FullName = ProductUserVM.ApplicationUser.FullName,
                    Email = ProductUserVM.ApplicationUser.Email,
                    PhoneNumber = ProductUserVM.ApplicationUser.PhoneNumber,
                    OrderDate = DateTime.Now,
                    OrderStatus = WC.StatusPending
                };

                _orderHRepo.Add(orderHeader);
                _orderHRepo.Save();

                foreach (var prod in ProductUserVM.ProductList)
                {
                    OrderDetail orderDetail = new OrderDetail()
                    {
                        OrderHeaderId = orderHeader.Id,
                        PricePerSqFt = prod.Price,
                        Sqft = prod.TempSqFt,
                        ProductId = prod.Id
                    };
                    _orderDRepo.Add(orderDetail);
                }
                _orderDRepo.Save();

                string nonceFromTheClient = collection["payment_method_nonce"];

                var request = new TransactionRequest
                {
                    Amount = Convert.ToDecimal(orderHeader.FinalOrderTotal),
                    PaymentMethodNonce = nonceFromTheClient,
                    OrderId = orderHeader.Id.ToString(),
                    Options = new TransactionOptionsRequest
                    {
                        SubmitForSettlement = true
                    }
                };

                var gateway = _brain.GetGateway();
                Result<Transaction> result = gateway.Transaction.Sale(request);

                if(result.Target?.ProcessorResponseText == "Approved")
                {
                    orderHeader.TransactionId = result.Target.Id;
                    orderHeader.OrderStatus = WC.StatusApproved;
                }
                else
                {
                    orderHeader.OrderStatus = WC.StatusCancelled;
                }
                _orderHRepo.Save();

                return RedirectToAction(nameof(InquiryConfirmation), new { id = orderHeader.Id });
            }
            else
            {
                //we need to create an inquiry
                var pathToTemplate = _webHostEnviroment.WebRootPath + Path.DirectorySeparatorChar.ToString() + "templates" + Path.DirectorySeparatorChar.ToString() + "Inquiry.html";

                var subect = "New Inquiry";
                string htmlBody = "";

                using (StreamReader sr = System.IO.File.OpenText(pathToTemplate))
                {
                    htmlBody = sr.ReadToEnd();
                }

                StringBuilder productListSB = new StringBuilder();
                foreach (var prod in ProductUserVM.ProductList)
                {
                    productListSB.Append($" - Name: {prod.Name} <span style='font-size:14px;'> (ID: {prod.Id}) </span><br />");
                }

                string messageBody = string.Format(htmlBody,
                                                ProductUserVM.ApplicationUser.FullName,
                                                ProductUserVM.ApplicationUser.Email,
                                                ProductUserVM.ApplicationUser.PhoneNumber,
                                                productListSB.ToString());

                await _emailSender.SendEmailAsync(WC.EmailAdmin, subect, messageBody);

                InquiryHeader inquiryHeader = new InquiryHeader()
                {
                    ApplicationUserId = claim.Value,
                    FullName = ProductUserVM.ApplicationUser.FullName,
                    Email = ProductUserVM.ApplicationUser.Email,
                    PhoneNumber = ProductUserVM.ApplicationUser.PhoneNumber,
                    InquiryDate = DateTime.Now
                };

                _inqHRepo.Add(inquiryHeader);
                _inqHRepo.Save();

                foreach (var prod in ProductUserVM.ProductList)
                {
                    InquiryDetail inquiryDetail = new InquiryDetail()
                    {
                        InquiryHeaderId = inquiryHeader.Id,
                        ProductId = prod.Id
                    };
                    _inqDRepo.Add(inquiryDetail);
                }
                _inqDRepo.Save();
                TempData[WC.Success] = "Inquiry submitted successfully!";
            }

            return RedirectToAction(nameof(InquiryConfirmation));
        }

        public IActionResult InquiryConfirmation(int id = 0)
        {
            OrderHeader orderHeader = null;
            if(id > 0)
            {
                orderHeader = _orderHRepo.FirstOrDefault(filter: u => u.Id == id);
            }
            HttpContext.Session.Clear();

            return View(orderHeader);
        }

        public IActionResult Remove(int id)
        {
            List<ShoppingCart> shoppingCartList = new List<ShoppingCart>();
            if (HttpContext.Session.Get<IEnumerable<ShoppingCart>>(WC.SessionCart) != null &&
                HttpContext.Session.Get<IEnumerable<ShoppingCart>>(WC.SessionCart).Count() > 0)
            {
                // sessions exists
                shoppingCartList = HttpContext.Session.Get<List<ShoppingCart>>(WC.SessionCart);
            }

            shoppingCartList.Remove(shoppingCartList.FirstOrDefault(u => u.ProductId == id));

            HttpContext.Session.Set(WC.SessionCart, shoppingCartList);

            TempData[WC.Success] = "Product removed from cart successfully!";

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult UpdateCart(IEnumerable<Product> prodList)
        {
            List<ShoppingCart> shoppingCartList = new List<ShoppingCart>();

            foreach(Product prod in prodList)
            {
                shoppingCartList.Add(new ShoppingCart { ProductId = prod.Id, SqFt = prod.TempSqFt });
            }

            HttpContext.Session.Set(WC.SessionCart, shoppingCartList);

            return RedirectToAction(nameof(Index));
        }

        public IActionResult Clear()
        {
            HttpContext.Session.Clear();

            return RedirectToAction("Index", "Home");
        }
    }
}
