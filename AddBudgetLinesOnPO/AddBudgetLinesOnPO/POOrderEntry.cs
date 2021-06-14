using System.Collections;
using System.Collections.Generic;
using PX.Data;
using PX.Objects.CS;
using PX.Objects.PM;

namespace PX.Objects.PO
{
    public class POOrderEntry_Extension : PXGraphExtension<POOrderEntry>
    {
		protected Dictionary<BudgetKeyTuple, PMCostBudget> costBudgets;

		#region Define & Delegate Data View
		[PXCopyPasteHiddenView]
		public PXSelect<PMCostBudget> AvailableCostBudget;

		public virtual IEnumerable availableCostBudget()
		{
			List<PMBudget> list = new List<PMBudget>();

			foreach (POLine line in Base.Transactions.Cache.Cached)
			{
				PMBudget row = PXSelectReadonly<PMBudget,
												Where<PMBudget.projectID, Equal<Required<POLine.projectID>>,
													And<PMBudget.projectTaskID, Equal<Required<POLine.taskID>>,
														And<PMBudget.inventoryID, Equal<Required<POLine.inventoryID>>,
															And<PMBudget.costCodeID, Equal<Required<POLine.costCodeID>>,
																And<PMBudget.type, Equal<GL.AccountType.expense>>>>>>>
												.Select(Base, line.ProjectID, line.TaskID, line.InventoryID, line.CostCodeID);
				if (row != null)
				{
					list.Add(row);
				}
			}

			HashSet<BudgetKeyTuple> existing = new HashSet<BudgetKeyTuple>();

			foreach (PMBudget row in list)
			{
				existing.Add(BudgetKeyTuple.Create(row));// row.GetBudgetKey());
			}

			foreach (PMBudget budget in GetCostBudget())
			{
				if (budget.Type != GL.AccountType.Expense)
					continue;

				if (existing.Contains(BudgetKeyTuple.Create(budget)) )//budget.GetBudgetKey()))
					budget.Selected = true;

				yield return budget;
			}
		}
		#endregion

		#region CacheAttached	
		[PXDBInt()]
		[PXDimension(ProjectTaskAttribute.DimensionName)]
		[PXSelector(typeof(Search<PMTask.taskID>), SubstituteKey = typeof(PMTask.taskCD))]
		protected void PMCostBudget_ProjectTaskID_CacheAttached(PXCache sender) { }
		#endregion

		#region Action Button
		public PXAction<POOrder> addCostBudget;
		[PXUIField(DisplayName = "Select Budget Lines")]
		public IEnumerable AddCostBudget(PXAdapter adapter)
		{
			if (AvailableCostBudget.View.AskExt() == WebDialogResult.OK)
			{
				AddSelectedCostBudget();
			}

			return adapter.Get();
		}

		public PXAction<POOrder> appendSelectedCostBudget;
		[PXUIField(DisplayName = "Add Lines")]
		[PXButton]
		public IEnumerable AppendSelectedCostBudget(PXAdapter adapter)
		{
			AddSelectedCostBudget();

			return adapter.Get();
		}
		#endregion

		#region Functions
		public virtual PMCostBudget GetOriginalCostBudget(BudgetKeyTuple record)
		{
			if (costBudgets == null)
			{
				costBudgets = BuildCostBudgetLookup();
			}

			PMCostBudget result = null;

			costBudgets.TryGetValue(record, out result);

			return result;
		}

		public virtual ICollection<PMCostBudget> GetCostBudget()
		{
			if (costBudgets == null)
			{
				costBudgets = BuildCostBudgetLookup();
			}

			return costBudgets.Values;
		}

		public virtual Dictionary<BudgetKeyTuple, PMCostBudget> BuildCostBudgetLookup()
		{
			Dictionary<BudgetKeyTuple, PMCostBudget> result = new Dictionary<BudgetKeyTuple, PMCostBudget>();

			var select = new PXSelectReadonly<PMCostBudget, 
									          Where<PMCostBudget.projectID, Equal<Current<POOrder.projectID>>, 
													And<PMCostBudget.type, Equal<GL.AccountType.expense>>>>(Base);

			foreach (PMCostBudget record in select.Select())
			{
				result.Add(BudgetKeyTuple.Create(record)/*record.GetBudgetKey()*/, record);
			}

			return result;
		}

		public virtual void AddSelectedCostBudget()
		{
			POLine pOline = (POLine)Base.Transactions.Cache.CreateInstance();

			Dictionary<POLineKey, POLine> poLines = new Dictionary<POLineKey, POLine>();

			foreach (POLine line in Base.Transactions.Cache.Cached)
			{
				poLines.Add(GetKey(line), line);
			}

			foreach (PMCostBudget budget in AvailableCostBudget.Cache.Updated)
			{
				if (budget.Type != GL.AccountType.Expense || budget.Selected != true) { continue; }

				pOline.ProjectID    = budget.ProjectID;
				pOline.InventoryID  = budget.InventoryID;
				pOline.TaskID       = budget.ProjectTaskID;
				pOline.CostCodeID   = budget.CostCodeID;
				pOline.OrderQty	    = budget.Qty;
				pOline.UOM		    = budget.UOM;
				pOline.CuryUnitCost = budget.CuryUnitRate;

				if (poLines.ContainsKey(GetKey(pOline)) == false)
				{
					Base.Transactions.Cache.Insert(pOline);
				}
			}
		}

		private POLineKey GetKey(POLine record)
		{
			return new POLineKey(record.InventoryID.Value, record.ProjectID.Value, record.TaskID.Value);
		}
		#endregion
	}

	public struct POLineKey
	{ 
		public readonly int InventoryID;
		public readonly int ProjectID;
		public readonly int TaskID;

		public POLineKey(int inventoryID, int projectID, int taskID)
		{
			InventoryID = inventoryID;
			ProjectID = projectID;
			TaskID = taskID;
		}
	}
}