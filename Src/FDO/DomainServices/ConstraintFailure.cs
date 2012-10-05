// --------------------------------------------------------------------------------------------
// Copyright (C) 2004 SIL International. All rights reserved.
//
// Distributable under the terms of either the Common Public License or the
// GNU Lesser General Public License, as specified in the LICENSING.txt file.
//
// File: ConstraintFailure.cs
// Originator: John Hatton
// Last reviewed: never
//
//
// <remarks>
// </remarks>
// --------------------------------------------------------------------------------------------
using SIL.FieldWorks.Common.COMInterfaces;
using SIL.FieldWorks.Common.FwUtils;
using SIL.CoreImpl;

namespace SIL.FieldWorks.FDO.DomainServices
{
	/// <summary>
	/// information about why a constraint failed
	/// </summary>
	public class ConstraintFailure
	{
		private readonly ICmObject m_object;
		private readonly int m_flid;
		private readonly string m_explanation;
		private readonly FdoCache m_cache;
		private string m_xmlDescription;

		//	protected string m_helpId;

		/// <summary>
		/// constructor using a simple string explanation. Also creates an annotation on the object.
		/// </summary>
		public ConstraintFailure(ICmObject problemObject, int flid, string explanation, bool createAnnotation)
		{
			m_cache = problemObject.Cache;
			m_object = problemObject;
			m_flid = flid;
			m_explanation = explanation;
//			m_explanation = new StText();
//			StTxtParaBldr paraBldr = new StTxtParaBldr(m_cache);
//			//review: I have no idea what this is as to be
//			paraBldr.ParaProps = StyleUtils.ParaStyleTextProps("Paragraph");
//			//todo: this pretends that the default analysis writing system is also the user interface 1.
//			//but I don't really know what's the right thing to do.
//			paraBldr.AppendRun(m_explanation, StyleUtils.CharStyleTextProps(null, m_cache.DefaultAnalWs));
//			paraBldr.CreateParagraph(annotation.TextOAHvo);

			//we do this because, if it is missing this annotation, then we really should annotate it
			//so that when the user goes looking to see why something didn't work, the error message
			//is sure to be there.
			//enhance: we are wasting time building an annotation repeatedly, if the correct one is already there
			if (createAnnotation)
				MakeAnnotation();
		}

		/// <summary>
		/// optional XML that can be used to highlight or describe the particular problem in an object.
		/// corresponds to the annotation field CompDetails
		/// </summary>
		public string XmlDescription
		{
			get
			{
				return m_xmlDescription;
			}
			set
			{
				m_xmlDescription = value;
			}
		}

		/// <summary>
		/// generate message to display to the user describing the problem
		/// </summary>
		/// <returns>a message</returns>
		public string GetMessage()
		{
			return !string.IsNullOrEmpty(m_explanation) ? m_explanation : Strings.ksNoExplanation;
		}

		/// <summary>
		/// attach an annotation describing this failure to the object. *Does Not* remove previous annotations!
		/// </summary>
		/// <remarks> I say it does not remove previous annotations because I haven't thought about how much smarts
		///  it would take to only remove once associated with this particular failure. So I am stipulating for now that
		///  the caller should first remove all of the kinds of indications which it might create.</remarks>
		/// <returns></returns>
		protected ICmBaseAnnotation MakeAnnotation()
		{
			//	callar should do something like this:CmBaseAnnotation.RemoveAnnotationsForObject(m_object.Cache, m_object.Hvo);

			var annotation = m_cache.ServiceLocator.GetInstance<ICmBaseAnnotationFactory>().Create();
			m_cache.LanguageProject.AnnotationsOC.Add(annotation);
			annotation.CompDetails = m_xmlDescription;

			annotation.TextOA = m_cache.ServiceLocator.GetInstance<IStTextFactory>().Create();
			var paraBldr = new StTxtParaBldr(m_cache);
			//review: I have no idea what this has to be.
			paraBldr.ParaStyleName = "Paragraph";
			//todo: this pretends that the default analysis writing system is also the user
			// interface 1.  but I don't really know what's the right thing to do.
			paraBldr.AppendRun(m_explanation,
				StyleUtils.CharStyleTextProps(null,
				m_cache.DefaultAnalWs));
			paraBldr.CreateParagraph(annotation.TextOA);

			annotation.BeginObjectRA = m_object;
			annotation.Flid = m_flid;
			annotation.CompDetails = m_xmlDescription;
			annotation.SourceRA = m_cache.LanguageProject.ConstraintCheckerAgent;
			return annotation;
		}
	}
}