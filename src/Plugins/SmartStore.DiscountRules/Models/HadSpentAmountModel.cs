﻿using System.Collections.Generic;
using System.Web.Mvc;
using SmartStore.Web.Framework;

namespace SmartStore.DiscountRules.Models
{
	public class HadSpentAmountModel : DiscountRuleModelBase
    {
		[SmartResourceDisplayName("Plugins.DiscountRules.HadSpentAmount.Fields.Amount")]
		public decimal SpentAmount { get; set; }

		[SmartResourceDisplayName("Plugins.DiscountRules.HadSpentAmount.Fields.LimitToCurrentBasketSubTotal")]
		public bool LimitToCurrentBasketSubTotal { get; set; }

		[SmartResourceDisplayName("Plugins.DiscountRules.HadSpentAmount.Fields.BasketSubTotalIncludesDiscounts")]
		public bool BasketSubTotalIncludesDiscounts { get; set; }
    }
}