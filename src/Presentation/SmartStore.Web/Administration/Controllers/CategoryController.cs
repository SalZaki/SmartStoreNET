﻿using System;
using System.Linq;
using System.Web.Mvc;
using SmartStore.Admin.Models.Catalog;
using SmartStore.Core;
using SmartStore.Core.Domain.Catalog;
using SmartStore.Core.Domain.Common;
using SmartStore.Core.Domain.Discounts;
using SmartStore.Services.Catalog;
using SmartStore.Services.Customers;
using SmartStore.Services.Discounts;
using SmartStore.Services.ExportImport;
using SmartStore.Services.Helpers;
using SmartStore.Services.Localization;
using SmartStore.Core.Logging;
using SmartStore.Services.Media;
using SmartStore.Services.Security;
using SmartStore.Services.Seo;
using SmartStore.Services.Stores;
using SmartStore.Web.Framework;
using SmartStore.Web.Framework.Controllers;
using SmartStore.Web.Framework.Mvc;
using Telerik.Web.Mvc;
using Telerik.Web.Mvc.UI;
using SmartStore.Core.Events;

namespace SmartStore.Admin.Controllers
{
    [AdminAuthorize]
    public partial class CategoryController : AdminControllerBase
    {
        #region Fields

        private readonly ICategoryService _categoryService;
        private readonly ICategoryTemplateService _categoryTemplateService;
        private readonly IManufacturerService _manufacturerService;
        private readonly IProductService _productService; 
        private readonly ICustomerService _customerService;
        private readonly IUrlRecordService _urlRecordService;
        private readonly IPictureService _pictureService;
        private readonly ILanguageService _languageService;
        private readonly ILocalizationService _localizationService;
        private readonly ILocalizedEntityService _localizedEntityService;
        private readonly IDiscountService _discountService;
        private readonly IPermissionService _permissionService;
        private readonly IAclService _aclService;
		private readonly IStoreService _storeService;
		private readonly IStoreMappingService _storeMappingService;
        private readonly IExportManager _exportManager;
        private readonly IWorkContext _workContext;
        private readonly ICustomerActivityService _customerActivityService;
		private readonly IDateTimeHelper _dateTimeHelper;
        private readonly AdminAreaSettings _adminAreaSettings;
        private readonly CatalogSettings _catalogSettings;
		private readonly IEventPublisher _eventPublisher;

        #endregion

        #region Constructors

        public CategoryController(ICategoryService categoryService, ICategoryTemplateService categoryTemplateService,
            IManufacturerService manufacturerService, IProductService productService, 
            ICustomerService customerService,
            IUrlRecordService urlRecordService, IPictureService pictureService, ILanguageService languageService,
            ILocalizationService localizationService, ILocalizedEntityService localizedEntityService,
            IDiscountService discountService, IPermissionService permissionService,
			IAclService aclService, IStoreService storeService, IStoreMappingService storeMappingService,
            IExportManager exportManager, IWorkContext workContext,
            ICustomerActivityService customerActivityService,
			IDateTimeHelper dateTimeHelper,
			AdminAreaSettings adminAreaSettings,
            CatalogSettings catalogSettings,
			IEventPublisher eventPublisher)
        {
            this._categoryService = categoryService;
            this._categoryTemplateService = categoryTemplateService;
            this._manufacturerService = manufacturerService;
            this._productService = productService;
            this._customerService = customerService;
            this._urlRecordService = urlRecordService;
            this._pictureService = pictureService;
            this._languageService = languageService;
            this._localizationService = localizationService;
            this._localizedEntityService = localizedEntityService;
            this._discountService = discountService;
            this._permissionService = permissionService;
            this._aclService = aclService;
			this._storeService = storeService;
			this._storeMappingService = storeMappingService;
            this._exportManager = exportManager;
            this._workContext = workContext;
            this._customerActivityService = customerActivityService;
			this._dateTimeHelper = dateTimeHelper;
            this._adminAreaSettings = adminAreaSettings;
            this._catalogSettings = catalogSettings;
			this._eventPublisher = eventPublisher;
        }

        #endregion

        #region Utilities

        [NonAction]
        protected void UpdateLocales(Category category, CategoryModel model)
        {
            foreach (var localized in model.Locales)
            {
                _localizedEntityService.SaveLocalizedValue(category,
                                                               x => x.Name,
                                                               localized.Name,
                                                               localized.LanguageId);

                _localizedEntityService.SaveLocalizedValue(category,
                                                           x => x.Description,
                                                           localized.Description,
                                                           localized.LanguageId);

                _localizedEntityService.SaveLocalizedValue(category,
                                                           x => x.MetaKeywords,
                                                           localized.MetaKeywords,
                                                           localized.LanguageId);

                _localizedEntityService.SaveLocalizedValue(category,
                                                           x => x.MetaDescription,
                                                           localized.MetaDescription,
                                                           localized.LanguageId);

                _localizedEntityService.SaveLocalizedValue(category,
                                                           x => x.MetaTitle,
                                                           localized.MetaTitle,
                                                           localized.LanguageId);

                //search engine name
				// codehint: sm-edit
                var seName = category.ValidateSeName(localized.SeName, localized.Name, false, localized.LanguageId);
                _urlRecordService.SaveSlug(category, seName, localized.LanguageId);
            }
        }

        [NonAction]
        protected void UpdatePictureSeoNames(Category category)
        {
            var picture = _pictureService.GetPictureById(category.PictureId.GetValueOrDefault());
            if (picture != null)
                _pictureService.SetSeoFilename(picture.Id, _pictureService.GetPictureSeName(category.Name));
        }

        [NonAction]
        protected void PrepareTemplatesModel(CategoryModel model)
        {
            if (model == null)
                throw new ArgumentNullException("model");

            var templates = _categoryTemplateService.GetAllCategoryTemplates();
            foreach (var template in templates)
            {
                model.AvailableCategoryTemplates.Add(new SelectListItem()
                {
                    Text = template.Name,
                    Value = template.Id.ToString()
                });
            }
        }

        [NonAction]
        protected void PrepareCategoryModel(CategoryModel model, Category category, bool excludeProperties)
        {
            if (model == null)
                throw new ArgumentNullException("model");

            var discounts = _discountService.GetAllDiscounts(DiscountType.AssignedToCategories, null, true);
            model.AvailableDiscounts = discounts.ToList();

            if (!excludeProperties)
            {
                model.SelectedDiscountIds = category.AppliedDiscounts.Select(d => d.Id).ToArray();
            }

			if (category != null)
			{
				model.CreatedOn = _dateTimeHelper.ConvertToUserTime(category.CreatedOnUtc, DateTimeKind.Utc);
				model.UpdatedOn = _dateTimeHelper.ConvertToUserTime(category.UpdatedOnUtc, DateTimeKind.Utc);
			}
        }

        [NonAction]
        private void PrepareAclModel(CategoryModel model, Category category, bool excludeProperties)
        {
            if (model == null)
                throw new ArgumentNullException("model");

            model.AvailableCustomerRoles = _customerService
                .GetAllCustomerRoles(true)
                .Select(cr => cr.ToModel())
                .ToList();
            if (!excludeProperties)
            {
                if (category != null)
                {
                    model.SelectedCustomerRoleIds = _aclService.GetCustomerRoleIdsWithAccess(category);
                }
                else
                {
                    model.SelectedCustomerRoleIds = new int[0];
                }
            }
        }

        [NonAction]
        protected void SaveCategoryAcl(Category category, CategoryModel model)
        {
            var existingAclRecords = _aclService.GetAclRecords(category);
            var allCustomerRoles = _customerService.GetAllCustomerRoles(true);
            foreach (var customerRole in allCustomerRoles)
            {
                if (model.SelectedCustomerRoleIds != null && model.SelectedCustomerRoleIds.Contains(customerRole.Id))
                {
                    //new role
                    if (existingAclRecords.Where(acl => acl.CustomerRoleId == customerRole.Id).Count() == 0)
                        _aclService.InsertAclRecord(category, customerRole.Id);
                }
                else
                {
                    //removed role
                    var aclRecordToDelete = existingAclRecords.Where(acl => acl.CustomerRoleId == customerRole.Id).FirstOrDefault();
                    if (aclRecordToDelete != null)
                        _aclService.DeleteAclRecord(aclRecordToDelete);
                }
            }
        }

		[NonAction]
		private void PrepareStoresMappingModel(CategoryModel model, Category category, bool excludeProperties)
		{
			if (model == null)
				throw new ArgumentNullException("model");

			model.AvailableStores = _storeService
				.GetAllStores()
				.Select(s => s.ToModel())
				.ToList();
			if (!excludeProperties)
			{
				if (category != null)
				{
					model.SelectedStoreIds = _storeMappingService.GetStoresIdsWithAccess(category);
				}
				else
				{
					model.SelectedStoreIds = new int[0];
				}
			}
		}

		[NonAction]
		protected void SaveStoreMappings(Category category, CategoryModel model)
		{
			var existingStoreMappings = _storeMappingService.GetStoreMappings(category);
			var allStores = _storeService.GetAllStores();
			foreach (var store in allStores)
			{
				if (model.SelectedStoreIds != null && model.SelectedStoreIds.Contains(store.Id))
				{
					//new role
					if (existingStoreMappings.Where(sm => sm.StoreId == store.Id).Count() == 0)
						_storeMappingService.InsertStoreMapping(category, store.Id);
				}
				else
				{
					//removed role
					var storeMappingToDelete = existingStoreMappings.Where(sm => sm.StoreId == store.Id).FirstOrDefault();
					if (storeMappingToDelete != null)
						_storeMappingService.DeleteStoreMapping(storeMappingToDelete);
				}
			}
		}

        #endregion

        #region List / tree

        public ActionResult Index()
        {
            return RedirectToAction("List");
        }

        public ActionResult List()
        {
            if (!_permissionService.Authorize(StandardPermissionProvider.ManageCatalog))
                return AccessDeniedView();

            var model = new CategoryListModel();
            var categories = _categoryService.GetAllCategories(null, 0, _adminAreaSettings.GridPageSize, true);
            var mappedCategories = categories.ToDictionary(x => x.Id);
            model.Categories = new GridModel<CategoryModel>
            {
                Data = categories.Select(x =>
                {
                    var categoryModel = x.ToModel();
                    categoryModel.Breadcrumb = x.GetCategoryBreadCrumb(_categoryService, mappedCategories);
                    return categoryModel;
                }),
                Total = categories.TotalCount
            };
            return View(model);
        }

        [HttpPost, GridAction(EnableCustomBinding = true)]
        public ActionResult List(GridCommand command, CategoryListModel model)
        {
            if (!_permissionService.Authorize(StandardPermissionProvider.ManageCatalog))
                return AccessDeniedView();

            var categories = _categoryService.GetAllCategories(model.SearchCategoryName, command.Page - 1, command.PageSize, true, model.SearchAlias);
            var mappedCategories = categories.ToDictionary(x => x.Id);
            var gridModel = new GridModel<CategoryModel>
            {
                Data = categories.Select(x =>
                {
                    var categoryModel = x.ToModel();
                    categoryModel.Breadcrumb = x.GetCategoryBreadCrumb(_categoryService, mappedCategories);
                    return categoryModel;
                }),
                Total = categories.TotalCount
            };
            return new JsonResult
            {
                Data = gridModel
            };
        }

        //ajax
        // codehint: sm-edit
        public ActionResult AllCategories(string label, int selectedId)
        {
            var categories = _categoryService.GetAllCategories(showHidden: true);
            var mappedCategories = categories.ToDictionary(x => x.Id);
            // codehint: sm-edit
            if (label.HasValue())
            {
                categories.Insert(0, new Category { Name = label, Id = 0 });
            }

            // codehint: sm-edit
            var list = from c in categories
                       select new { 
                           id = c.Id.ToString(),
                           text = c.GetCategoryBreadCrumb(_categoryService, mappedCategories), 
                           selected = c.Id == selectedId 
                       };

            // codehint: sm-edit
            return new JsonResult { Data = list.ToList(), JsonRequestBehavior = JsonRequestBehavior.AllowGet };
        }

        public ActionResult Tree()
        {
            if (!_permissionService.Authorize(StandardPermissionProvider.ManageCatalog))
                return AccessDeniedView();

            var rootCategories = _categoryService.GetAllCategoriesByParentCategoryId(0, true);
            return View(rootCategories);
        }

        //ajax
        [AcceptVerbs(HttpVerbs.Post)]
        public ActionResult TreeLoadChildren(TreeViewItem node)
        {
            var parentId = !string.IsNullOrEmpty(node.Value) ? Convert.ToInt32(node.Value) : 0;
			var urlHelper = new UrlHelper(this.ControllerContext.RequestContext);

            var children = _categoryService.GetAllCategoriesByParentCategoryId(parentId, true).Select(x =>
			{
				var childCount = _categoryService.GetAllCategoriesByParentCategoryId(x.Id, true).Count;
				string text = (childCount > 0 ? "{0} ({1})".FormatWith(x.Name, childCount) : x.Name);

                var item = new TreeViewItem
                {
                    Text = x.Alias.HasValue() ? "{0} <span class='label'>{1}</span>".FormatCurrent(text, x.Alias) : text,
                    Encoded = x.Alias.IsEmpty(),
                    Value = x.Id.ToString(),
                    LoadOnDemand = (childCount > 0),
                    Enabled = true,
                    ImageUrl = Url.Content(x.Published ? "~/Administration/Content/images/ico-content.png" : "~/Administration/Content/images/ico-content-o60.png"),
					Url = urlHelper.Action("Edit", "Category", new { id = x.Id })
                };
                return item;
            });

            return new JsonResult { Data = children };
        }

        //ajax
        public ActionResult TreeDrop(int item, int destinationitem, string position)
        {
            var categoryItem = _categoryService.GetCategoryById(item);
            var categoryDestinationItem = _categoryService.GetCategoryById(destinationitem);

            #region Re-calculate all display orders
            switch (position)
            {
                case "over":
                    categoryItem.ParentCategoryId = categoryDestinationItem.Id;
                    break;
                case "before":
                case "after":
                    categoryItem.ParentCategoryId = categoryDestinationItem.ParentCategoryId;
                    break;
            }
            //update display orders
            int tmp = 0;
            foreach (var c in _categoryService.GetAllCategoriesByParentCategoryId(categoryItem.ParentCategoryId, true))
            {
                c.DisplayOrder = tmp;
                tmp += 10;
                _categoryService.UpdateCategory(c);

                switch (position)
                {
                    case "before":
                        categoryItem.DisplayOrder = categoryDestinationItem.DisplayOrder - 5;
                        break;
                    case "after":
                        categoryItem.DisplayOrder = categoryDestinationItem.DisplayOrder + 5;
                        break;
                }
            }
            #endregion

            #region Simple Sort method (Obsolete, has issues)
            //switch (position)
            //{
            //    case "over":
            //        categoryItem.ParentCategoryId = categoryDestinationItem.Id;
            //        break;
            //    case "before":
            //        categoryItem.ParentCategoryId = categoryDestinationItem.ParentCategoryId;
            //        categoryItem.DisplayOrder = categoryDestinationItem.DisplayOrder - 1;
            //        break;
            //    case "after":
            //        categoryItem.ParentCategoryId = categoryDestinationItem.ParentCategoryId;
            //        categoryItem.DisplayOrder = categoryDestinationItem.DisplayOrder + 1;
            //        break;
            //}
            #endregion

            _categoryService.UpdateCategory(categoryItem);

            return Json(new { success = true });
        }

        #endregion

        #region Create / Edit / Delete

        public ActionResult Create()
        {
            if (!_permissionService.Authorize(StandardPermissionProvider.ManageCatalog))
                return AccessDeniedView();

            var model = new CategoryModel();
            //parent categories
            // codehint: sm-delete
            //locales
            AddLocales(_languageService, model.Locales);
            //templates
            PrepareTemplatesModel(model);
            PrepareCategoryModel(model, null, true);
            //ACL
            PrepareAclModel(model, null, false);
			//Stores
			PrepareStoresMappingModel(model, null, false);
            //default values
            model.PageSize = 12; // codehint: sm-edit > 4;
            model.Published = true;

            model.AllowCustomersToSelectPageSize = true;
            //model.PageSizeOptions = _catalogSettings.DefaultPageSizeOptions; // codehint: sm-edit > _catalogSettings.DefaultCategoryPageSizeOptions;

            return View(model);
        }

        [HttpPost, ParameterBasedOnFormNameAttribute("save-continue", "continueEditing")]
		[ValidateInput(false)]
        public ActionResult Create(CategoryModel model, bool continueEditing, FormCollection form)
        {
            if (!_permissionService.Authorize(StandardPermissionProvider.ManageCatalog))
                return AccessDeniedView();

            if (ModelState.IsValid)
            {
                var category = model.ToEntity();
                category.CreatedOnUtc = DateTime.UtcNow;
                category.UpdatedOnUtc = DateTime.UtcNow;
                _categoryService.InsertCategory(category);
                //search engine name
                model.SeName = category.ValidateSeName(model.SeName, category.Name, true);
                _urlRecordService.SaveSlug(category, model.SeName, 0);
                //locales
                UpdateLocales(category, model);
                //disounts
                var allDiscounts = _discountService.GetAllDiscounts(DiscountType.AssignedToCategories, null, true);
                foreach (var discount in allDiscounts)
                {
                    if (model.SelectedDiscountIds != null && model.SelectedDiscountIds.Contains(discount.Id))
                        category.AppliedDiscounts.Add(discount);
                }
                _categoryService.UpdateCategory(category);
                //update "HasDiscountsApplied" property
                _categoryService.UpdateHasDiscountsApplied(category);
                //update picture seo file name
                UpdatePictureSeoNames(category);
                //ACL (customer roles)
                SaveCategoryAcl(category, model);
				//Stores
				SaveStoreMappings(category, model);

				_eventPublisher.Publish(new ModelBoundEvent(model, category, form));

                //activity log
                _customerActivityService.InsertActivity("AddNewCategory", _localizationService.GetResource("ActivityLog.AddNewCategory"), category.Name);

                NotifySuccess(_localizationService.GetResource("Admin.Catalog.Categories.Added"));
                return continueEditing ? RedirectToAction("Edit", new { id = category.Id }) : RedirectToAction("List");
            }

            //If we got this far, something failed, redisplay form
            //templates
            PrepareTemplatesModel(model);
            //parent categories
            // codehint: sm-delete
            // codehint: sm-edit
            if (model.ParentCategoryId.HasValue)
            {
                // codehint: sm-edit
                var parentCategory = _categoryService.GetCategoryById(model.ParentCategoryId.Value);
                if (parentCategory != null && !parentCategory.Deleted)
                    model.ParentCategoryBreadcrumb = parentCategory.GetCategoryBreadCrumb(_categoryService);
                else
                    model.ParentCategoryId = 0;
            }

            PrepareCategoryModel(model, null, true);
            //ACL
            PrepareAclModel(model, null, true);
			//Stores
			PrepareStoresMappingModel(model, null, true);
            return View(model);
        }

        public ActionResult Edit(int id)
        {
            if (!_permissionService.Authorize(StandardPermissionProvider.ManageCatalog))
                return AccessDeniedView();

            var category = _categoryService.GetCategoryById(id);
            if (category == null || category.Deleted)
                //No category found with the specified id
                return RedirectToAction("List");

            var model = category.ToModel();
            //parent categories
            // codehint: sm-delete
            // codehint: sm-edit
            if (model.ParentCategoryId.HasValue)
            {
                // codehint: sm-edit
                var parentCategory = _categoryService.GetCategoryById(model.ParentCategoryId.Value);
                if (parentCategory != null && !parentCategory.Deleted)
                    model.ParentCategoryBreadcrumb = parentCategory.GetCategoryBreadCrumb(_categoryService);
                else
                    model.ParentCategoryId = 0;
            }
            //locales
            AddLocales(_languageService, model.Locales, (locale, languageId) =>
            {
                locale.Name = category.GetLocalized(x => x.Name, languageId, false, false);
                locale.Description = category.GetLocalized(x => x.Description, languageId, false, false);
                locale.MetaKeywords = category.GetLocalized(x => x.MetaKeywords, languageId, false, false);
                locale.MetaDescription = category.GetLocalized(x => x.MetaDescription, languageId, false, false);
                locale.MetaTitle = category.GetLocalized(x => x.MetaTitle, languageId, false, false);
                locale.SeName = category.GetSeName(languageId, false, false);
            });
            //templates
            PrepareTemplatesModel(model);
            PrepareCategoryModel(model, category, false);
            //ACL
            PrepareAclModel(model, category, false);
			//Stores
			PrepareStoresMappingModel(model, category, false);

            return View(model);
        }

        [HttpPost, ParameterBasedOnFormNameAttribute("save-continue", "continueEditing")]
		[ValidateInput(false)]
        public ActionResult Edit(CategoryModel model, bool continueEditing, FormCollection form)
        {
            if (!_permissionService.Authorize(StandardPermissionProvider.ManageCatalog))
                return AccessDeniedView();

            var category = _categoryService.GetCategoryById(model.Id);
            if (category == null || category.Deleted)
                //No category found with the specified id
                return RedirectToAction("List");

            if (ModelState.IsValid)
            {
				int prevPictureId = category.PictureId.GetValueOrDefault();
                category = model.ToEntity(category);
                category.UpdatedOnUtc = DateTime.UtcNow;
                _categoryService.UpdateCategory(category);
                //search engine name
                model.SeName = category.ValidateSeName(model.SeName, category.Name, true);
                _urlRecordService.SaveSlug(category, model.SeName, 0);
                //locales
                UpdateLocales(category, model);
                //discounts
                var allDiscounts = _discountService.GetAllDiscounts(DiscountType.AssignedToCategories, null, true);
                foreach (var discount in allDiscounts)
                {
                    if (model.SelectedDiscountIds != null && model.SelectedDiscountIds.Contains(discount.Id))
                    {
                        //new role
                        if (category.AppliedDiscounts.Where(d => d.Id == discount.Id).Count() == 0)
                            category.AppliedDiscounts.Add(discount);
                    }
                    else
                    {
                        //removed role
                        if (category.AppliedDiscounts.Where(d => d.Id == discount.Id).Count() > 0)
                            category.AppliedDiscounts.Remove(discount);
                    }
                }
                _categoryService.UpdateCategory(category);
                //update "HasDiscountsApplied" property
                _categoryService.UpdateHasDiscountsApplied(category);
                //delete an old picture (if deleted or updated)
                if (prevPictureId > 0 && prevPictureId != category.PictureId)
                {
                    var prevPicture = _pictureService.GetPictureById(prevPictureId);
                    if (prevPicture != null)
                        _pictureService.DeletePicture(prevPicture);
                }
                //update picture seo file name
                UpdatePictureSeoNames(category);
                //ACL
                SaveCategoryAcl(category, model);
				//Stores
				SaveStoreMappings(category, model);

				_eventPublisher.Publish(new ModelBoundEvent(model, category, form));

                //activity log
                _customerActivityService.InsertActivity("EditCategory", _localizationService.GetResource("ActivityLog.EditCategory"), category.Name);

                NotifySuccess(_localizationService.GetResource("Admin.Catalog.Categories.Updated"));
                return continueEditing ? RedirectToAction("Edit", category.Id) : RedirectToAction("List");
            }


            //If we got this far, something failed, redisplay form
            //parent categories
            // codehint: sm-delete
            // codehint: sm-edit
            if (model.ParentCategoryId.HasValue)
            {
                // codehint: sm-edit
                var parentCategory = _categoryService.GetCategoryById(model.ParentCategoryId.Value);
                if (parentCategory != null && !parentCategory.Deleted)
                    model.ParentCategoryBreadcrumb = parentCategory.GetCategoryBreadCrumb(_categoryService);
                else
                    model.ParentCategoryId = 0;
            }
            //templates
            PrepareTemplatesModel(model);
            PrepareCategoryModel(model, category, true);
            //ACL
            PrepareAclModel(model, category, true);
			//Stores
			PrepareStoresMappingModel(model, category, true);

            return View(model);
        }

        [HttpPost]
		public ActionResult Delete(int id, string deleteType)
        {
            if (!_permissionService.Authorize(StandardPermissionProvider.ManageCatalog))
                return AccessDeniedView();

            var category = _categoryService.GetCategoryById(id);
            if (category == null)
                return RedirectToAction("List");

			_categoryService.DeleteCategory(category, deleteType.IsCaseInsensitiveEqual("deletechilds"));

            _customerActivityService.InsertActivity("DeleteCategory", _localizationService.GetResource("ActivityLog.DeleteCategory"), category.Name);

            NotifySuccess(_localizationService.GetResource("Admin.Catalog.Categories.Deleted"));
            return RedirectToAction("List");
        }


        #endregion

        #region Export / Import

        public ActionResult ExportXml()
        {
            if (!_permissionService.Authorize(StandardPermissionProvider.ManageCatalog))
                return AccessDeniedView();

            try
            {
                var fileName = string.Format("categories_{0}.xml", DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss"));
                var xml = _exportManager.ExportCategoriesToXml();
                return new XmlDownloadResult(xml, "categories.xml");
            }
            catch (Exception exc)
            {
                NotifyError(exc);
                return RedirectToAction("List");
            }
        }

        #endregion

        #region Products

        [HttpPost, GridAction(EnableCustomBinding = true)]
        public ActionResult ProductList(GridCommand command, int categoryId)
        {
            if (!_permissionService.Authorize(StandardPermissionProvider.ManageCatalog))
                return AccessDeniedView();
            
            var productCategories = _categoryService.GetProductCategoriesByCategoryId(categoryId,
                command.Page - 1, command.PageSize, true);

            var model = new GridModel<CategoryModel.CategoryProductModel>
            {
                Data = productCategories
                .Select(x =>
                {
					var product = _productService.GetProductById(x.ProductId);

                    return new CategoryModel.CategoryProductModel()
                    {
                        Id = x.Id,
                        CategoryId = x.CategoryId,
                        ProductId = x.ProductId,
                        ProductName = product.Name,
						Sku = product.Sku,
						ProductTypeName = product.GetProductTypeLabel(_localizationService),
						ProductTypeLabelHint = product.ProductTypeLabelHint,
						Published = product.Published,
                        IsFeaturedProduct = x.IsFeaturedProduct,
                        DisplayOrder1 = x.DisplayOrder
                    };
                }),
                Total = productCategories.TotalCount
            };

            return new JsonResult
            {
                Data = model
            };
        }

        [GridAction(EnableCustomBinding = true)]
        public ActionResult ProductUpdate(GridCommand command, CategoryModel.CategoryProductModel model)
        {
            if (!_permissionService.Authorize(StandardPermissionProvider.ManageCatalog))
                return AccessDeniedView();

            var productCategory = _categoryService.GetProductCategoryById(model.Id);
            if (productCategory == null)
                throw new ArgumentException("No product category mapping found with the specified id");

            productCategory.IsFeaturedProduct = model.IsFeaturedProduct;
            productCategory.DisplayOrder = model.DisplayOrder1;
            _categoryService.UpdateProductCategory(productCategory);

            return ProductList(command, productCategory.CategoryId);
        }

        [GridAction(EnableCustomBinding = true)]
        public ActionResult ProductDelete(int id, GridCommand command)
        {
            if (!_permissionService.Authorize(StandardPermissionProvider.ManageCatalog))
                return AccessDeniedView();

            var productCategory = _categoryService.GetProductCategoryById(id);
            if (productCategory == null)
                throw new ArgumentException("No product category mapping found with the specified id");

            var categoryId = productCategory.CategoryId;
            _categoryService.DeleteProductCategory(productCategory);

            return ProductList(command, categoryId);
        }

        public ActionResult ProductAddPopup(int categoryId)
        {
            if (!_permissionService.Authorize(StandardPermissionProvider.ManageCatalog))
                return AccessDeniedView();


            var ctx = new ProductSearchContext();
            ctx.LanguageId = _workContext.WorkingLanguage.Id;
            ctx.OrderBy = ProductSortingEnum.Position;
            ctx.PageSize = _adminAreaSettings.GridPageSize;
            ctx.ShowHidden = true;

            var products = _productService.SearchProducts(ctx);

            var model = new CategoryModel.AddCategoryProductModel();
            model.Products = new GridModel<ProductModel>
            {
                Data = products.Select(x => 
				{
					var productModel = x.ToModel();
					productModel.ProductTypeName = x.GetProductTypeLabel(_localizationService);

					return productModel;
				}),
                Total = products.TotalCount
            };
            // categories
            var allCategories = _categoryService.GetAllCategories(showHidden: true);
            var mappedCategories = allCategories.ToDictionary(x => x.Id);
            foreach (var c in allCategories)
            {
                model.AvailableCategories.Add(new SelectListItem() { Text = c.GetCategoryNameWithPrefix(_categoryService, mappedCategories), Value = c.Id.ToString() });
            }

            //manufacturers
            // model.AvailableManufacturers.Add(new SelectListItem() { Text = _localizationService.GetResource("Admin.Common.All"), Value = "0" }); // codehint: sm-delete
            foreach (var m in _manufacturerService.GetAllManufacturers(true))
                model.AvailableManufacturers.Add(new SelectListItem() { Text = m.Name, Value = m.Id.ToString() });

			//product types
			model.AvailableProductTypes = ProductType.SimpleProduct.ToSelectList(false).ToList();
			model.AvailableProductTypes.Insert(0, new SelectListItem() { Text = _localizationService.GetResource("Admin.Common.All"), Value = "0" });

            return View(model);
        }

        [HttpPost, GridAction(EnableCustomBinding = true)]
        public ActionResult ProductAddPopupList(GridCommand command, CategoryModel.AddCategoryProductModel model)
        {
            if (!_permissionService.Authorize(StandardPermissionProvider.ManageCatalog))
                return AccessDeniedView();

            var gridModel = new GridModel();
            var ctx = new ProductSearchContext();

            if (model.SearchCategoryId > 0)
                ctx.CategoryIds.Add(model.SearchCategoryId);

            ctx.ManufacturerId = model.SearchManufacturerId;
            ctx.Keywords = model.SearchProductName;
            ctx.LanguageId = _workContext.WorkingLanguage.Id;
            ctx.OrderBy = ProductSortingEnum.Position;
            ctx.PageIndex = command.Page - 1;
            ctx.PageSize = command.PageSize;
            ctx.ShowHidden = true;
			ctx.ProductType = model.SearchProductTypeId > 0 ? (ProductType?)model.SearchProductTypeId : null;

            var products = _productService.SearchProducts(ctx);
			gridModel.Data = products.Select(x =>
			{
				var productModel = x.ToModel();
				productModel.ProductTypeName = x.GetProductTypeLabel(_localizationService);

				return productModel;
			});

            gridModel.Total = products.TotalCount;
            return new JsonResult
            {
                Data = gridModel
            };
        }

        [HttpPost]
        [FormValueRequired("save")]
        public ActionResult ProductAddPopup(string btnId, string formId, CategoryModel.AddCategoryProductModel model)
        {
            if (!_permissionService.Authorize(StandardPermissionProvider.ManageCatalog))
                return AccessDeniedView();

            if (model.SelectedProductIds != null)
            {
                foreach (int id in model.SelectedProductIds)
                {
                    var product = _productService.GetProductById(id);
                    if (product != null)
                    {
                        var existingProductCategories = _categoryService.GetProductCategoriesByCategoryId(model.CategoryId, 0, int.MaxValue, true);
                        if (existingProductCategories.FindProductCategory(id, model.CategoryId) == null)
                        {
                            _categoryService.InsertProductCategory(
                                new ProductCategory()
                                {
                                    CategoryId = model.CategoryId,
                                    ProductId = id,
                                    IsFeaturedProduct = false,
                                    DisplayOrder = 1
                                });
                        }
                    }
                }
            }

            ViewBag.RefreshPage = true;
            ViewBag.btnId = btnId;
            ViewBag.formId = formId;
            model.Products = new GridModel<ProductModel>();
            return View(model);
        }

        #endregion
    }
}
