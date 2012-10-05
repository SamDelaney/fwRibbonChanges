﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using SIL.FieldWorks.Common.FwUtils;
using SIL.FieldWorks.Common.Widgets;
using SIL.FieldWorks.FDO;
using SIL.FieldWorks.FdoUi;
using XCore;
using SIL.FieldWorks.Common.Framework;
using Skybound.Gecko;

namespace SIL.FieldWorks.LexText.Controls
{

	[System.Runtime.InteropServices.ComVisible(true)]
	public class WebPageInteractor
	{
		private readonly HtmlControl m_htmlControl;
		private readonly Mediator m_mediator;
		private readonly FdoCache m_cache;
		private readonly FwTextBox m_tbWordForm;

		public WebPageInteractor(HtmlControl htmlControl, ParserTrace parserTrace, Mediator mediator, FwTextBox tbWordForm)
		{
			m_htmlControl = htmlControl;
			ParserTrace = parserTrace;
			m_mediator = mediator;
			m_cache = (FdoCache)m_mediator.PropertyTable.GetValue("cache");
			m_tbWordForm = tbWordForm;
#if __MonoCS__
			m_htmlControl.Browser.DomClick += HandleDomClick;
			m_htmlControl.Browser.DomMouseMove += HandleHtmlControlBrowserDomMouseMove;
#endif
		}

#if __MonoCS__
		protected GeckoElement GetParentTable(GeckoElement element)
		{
			while (element != null && element.TagName.ToLowerInvariant() != "table".ToLowerInvariant())
				element = element.Parent;

			return element;
		}

		protected string GetParameterFromJavaScriptFunctionCall(string javascript)
		{
			int start = javascript.IndexOf('(');
			int end = javascript.IndexOf(')');
			// omit any enclosing quotation marks
			if (javascript[start + 1] == '"')
				++start;
			if (javascript[end - 1] == '"')
				--end;
			return javascript.Substring(start + 1, end - start - 1);
		}

		protected void HandleHtmlControlBrowserDomMouseMove(object sender, GeckoDomMouseEventArgs e)
		{
			if (sender == null || e == null || e.Target == null)
				return;

			GeckoElement parentTable = GetParentTable(e.Target);

			if (parentTable == null)
				return;

			GeckoNode onMouseMove = parentTable.Attributes["onmousemove"];

			if (onMouseMove == null)
				return;

			MouseMove();
		}

		protected void HandleDomClick(object sender, GeckoDomEventArgs e)
		{
			if (sender == null || e == null || e.Target == null)
				return;

			GeckoElement parentTable = GetParentTable(e.Target);

			if (parentTable == null)
				return;

			GeckoNode onClick = parentTable.Attributes["onclick"];
			// The next two lines are needed to fix FWNX-725, but if they're not commented out, the
			// program silently disappears shortly after the TryAWordDlg dialog is closed *if this
			// method is ever invoked by clicking on the HTML control anywhere*.  Somehow, either
			// e.Target.Attributes["onclick"] or onClick.TextContent must set some state in the browser
			// that causes this horrendous behavior.
			//if (onClick == null)
			//	onClick = e.Target.Attributes["onclick"];
			if (onClick == null)
				return;

			var js = onClick.TextContent;
			if (js.Contains("JumpToToolBasedOnHvo"))
			{
				JumpToToolBasedOnHvo(Int32.Parse(GetParameterFromJavaScriptFunctionCall(js)));
			}
			if (js.Contains("ShowWordGrammarDetail") || js.Contains("ButtonShowWGDetails"))
			{
				ShowWordGrammarDetail(GetParameterFromJavaScriptFunctionCall(js));
			}
			if (js.Contains("TryWordGrammarAgain"))
			{
				TryWordGrammarAgain(GetParameterFromJavaScriptFunctionCall(js));
			}
			if (js.Contains("GoToPreviousWordGrammarPage"))
			{
				GoToPreviousWordGrammarPage();
			}
		}
#endif

		/// <summary>
		/// Set the current parser to use when tracing
		/// </summary>
		public ParserTrace ParserTrace { get; set; }

		/// <summary>
		/// Have the main FLEx window jump to the appropriate item
		/// </summary>
		/// <param name="hvo">item whose parent will indcate where to jump to</param>
		public void JumpToToolBasedOnHvo(int hvo)
		{
			if (hvo == 0)
				return;
			string sTool = null;
			int parentClassId = 0;
			ICmObject cmo = m_cache.ServiceLocator.GetObject(hvo);
			switch (cmo.ClassID)
			{
				case MoFormTags.kClassId:					// fall through
				case MoAffixAllomorphTags.kClassId:			// fall through
				case MoStemAllomorphTags.kClassId:			// fall through
				case MoInflAffMsaTags.kClassId:				// fall through
				case MoDerivAffMsaTags.kClassId:			// fall through
				case MoUnclassifiedAffixMsaTags.kClassId:	// fall through
				case MoStemMsaTags.kClassId:				// fall through
				case MoMorphSynAnalysisTags.kClassId:		// fall through
				case MoAffixProcessTags.kClassId:
					sTool = "lexiconEdit";
					parentClassId = LexEntryTags.kClassId;
					break;
				case MoInflAffixSlotTags.kClassId:		// fall through
				case MoInflAffixTemplateTags.kClassId:	// fall through
				case PartOfSpeechTags.kClassId:
					sTool = "posEdit";
					parentClassId = PartOfSpeechTags.kClassId;
					break;
				// still need to test compound rule ones
				case MoCoordinateCompoundTags.kClassId:	// fall through
				case MoEndoCompoundTags.kClassId:		// fall through
				case MoExoCompoundTags.kClassId:
					sTool = "compoundRuleAdvancedEdit";
					parentClassId = cmo.ClassID;
					break;
				case PhRegularRuleTags.kClassId:		// fall through
				case PhMetathesisRuleTags.kClassId:
					sTool = "PhonologicalRuleEdit";
					parentClassId = cmo.ClassID;
					break;
			}
			if (parentClassId <= 0)
				return; // do nothing
			cmo = CmObjectUi.GetSelfOrParentOfClass(cmo, parentClassId);
			if (cmo == null)
				return; // do nothing
			m_mediator.PostMessage("FollowLink", new FwLinkArgs(sTool, cmo.Guid));
		}

		/// <summary>
		/// Change mouse cursor to a hand when the mouse is moved over an object
		/// </summary>
		public void MouseMove()
		{
#if __MonoCS__ // Setting WinForm Cursor has no affect on GeckoFx.
			Cursor.Current = Cursors.Hand;
#endif
		}
		/// <summary>
		/// Show the first pass of the Word Grammar Debugger
		/// </summary>
		/// <param name="sNodeId">The node id in the XAmple trace to use</param>
		public void ShowWordGrammarDetail(string sNodeId)
		{
			string sForm = AdjustForm(m_tbWordForm.Text);
			m_htmlControl.URL = ParserTrace.SetUpWordGrammarDebuggerPage(sNodeId, sForm, m_htmlControl.URL);
		}
		/// <summary>
		/// Try another pass in the Word Grammar Debugger
		/// </summary>
		/// <param name="sNodeId">the node id of the step to try</param>
		public void TryWordGrammarAgain(string sNodeId)
		{
			string sForm = AdjustForm(m_tbWordForm.Text);
			m_htmlControl.URL = ParserTrace.PerformAnotherWordGrammarDebuggerStepPage(sNodeId, sForm, m_htmlControl.URL);
		}
		/// <summary>
		/// Back up a page in the Word Grammar Debugger
		/// </summary>
		/// <remarks>
		/// We cannot merely use the history mechanism of the html control
		/// because we need to keep track of the xml page source file as well as the html page.
		/// This info is kept in the WordGrammarStack.
		/// </remarks>
		public void GoToPreviousWordGrammarPage()
		{
			m_htmlControl.URL = ParserTrace.PopWordGrammarStack();
		}
		/// <summary>
		/// Modify the content of the form to use entities when needed
		/// </summary>
		/// <param name="sForm">form to adjust</param>
		/// <returns>adjusted form</returns>
		protected string AdjustForm(string sForm)
		{
			string sResult1 = sForm.Replace("&", "&amp;");
			string sResult2 = sResult1.Replace("<", "&lt;");
			return sResult2;
		}
	}
}