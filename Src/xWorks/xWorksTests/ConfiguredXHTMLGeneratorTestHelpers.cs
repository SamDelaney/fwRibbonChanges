﻿// Copyright (c) 2014-2017 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using SIL.CoreImpl;
using SIL.FieldWorks.Common.Controls;
using SIL.FieldWorks.Common.COMInterfaces;
using SIL.FieldWorks.FDO;
using SIL.FieldWorks.FDO.DomainServices;
using SIL.Utils;

namespace SIL.FieldWorks.XWorks
{
	public partial class ConfiguredXHTMLGeneratorTests
	{
		private static void DeleteTempXhtmlAndCssFiles(string xhtmlPath)
		{
			if (string.IsNullOrEmpty(xhtmlPath))
				return;
			File.Delete(xhtmlPath);
			File.Delete(Path.ChangeExtension(xhtmlPath, "css"));
		}

		/// <summary>Creates a DictionaryConfigurationModel with one Main and two Minor Entry nodes, all with enabled HeadWord children</summary>
		/// <param name="cache"></param>
		internal static DictionaryConfigurationModel CreateInterestingConfigurationModel(FdoCache cache)
		{
			var mainHeadwordNode = new ConfigurableDictionaryNode
			{
				FieldDescription = "HeadWord",
				CSSClassNameOverride = "entry",
				DictionaryNodeOptions = GetWsOptionsForLanguages(new[] { "fr" }),
				Before = "MainEntry: ",
			};
			var mainEntryNode = new ConfigurableDictionaryNode
			{
				Children = new List<ConfigurableDictionaryNode> { mainHeadwordNode },
				FieldDescription = "LexEntry",
			};
			CssGeneratorTests.PopulateFieldsForTesting(mainEntryNode);

			var minorEntryNode = mainEntryNode.DeepCloneUnderSameParent();
			minorEntryNode.CSSClassNameOverride = "minorentry";
			minorEntryNode.Before = "MinorEntry: ";
			minorEntryNode.DictionaryNodeOptions = GetFullyEnabledListOptions(cache, DictionaryNodeListOptions.ListIds.Complex);

			var minorSecondNode = minorEntryNode.DeepCloneUnderSameParent();
			minorSecondNode.Before = "HalfStep: ";
			minorEntryNode.DictionaryNodeOptions = GetFullyEnabledListOptions(cache, DictionaryNodeListOptions.ListIds.Variant);

			return new DictionaryConfigurationModel
			{
				AllPublications = true,
				Parts = new List<ConfigurableDictionaryNode> { mainEntryNode, minorEntryNode, minorSecondNode }
			};
		}

		private static ConfigurableDictionaryNode CreatePictureModel()
		{
			var thumbNailNode = new ConfigurableDictionaryNode
			{
				FieldDescription = "PictureFileRA",
				CSSClassNameOverride = "picture"
			};
			var pictureNode = new ConfigurableDictionaryNode
			{
				FieldDescription = "PicturesOfSenses",
				CSSClassNameOverride = "Pictures",
				Children = new List<ConfigurableDictionaryNode> { thumbNailNode }
			};
			var sensesNode = new ConfigurableDictionaryNode { FieldDescription = "Senses" };
			var mainEntryNode = new ConfigurableDictionaryNode
			{
				Children = new List<ConfigurableDictionaryNode> { sensesNode, pictureNode },
				FieldDescription = "LexEntry"
			};
			CssGeneratorTests.PopulateFieldsForTesting(mainEntryNode);
			return mainEntryNode;
		}

		/// <summary>
		/// Creates an ILexEntry object, optionally with specified headword and gloss
		/// </summary>
		/// <param name="cache"></param>
		/// <param name="headword">Optional: defaults to 'Citation'</param>
		/// <param name="gloss">Optional: defaults to 'gloss'</param>
		/// <returns></returns>
		internal static ILexEntry CreateInterestingLexEntry(FdoCache cache, string headword = "Citation", string gloss = "gloss")
		{
			var factory = cache.ServiceLocator.GetInstance<ILexEntryFactory>();
			var entry = factory.Create();
			cache.LangProject.AddToCurrentAnalysisWritingSystems(
				cache.WritingSystemFactory.get_Engine("en") as IWritingSystem);
			cache.LangProject.AddToCurrentVernacularWritingSystems(
				cache.WritingSystemFactory.get_Engine("fr") as IWritingSystem);
			var wsEn = cache.WritingSystemFactory.GetWsFromStr("en");
			var wsFr = cache.WritingSystemFactory.GetWsFromStr("fr");
			AddHeadwordToEntry(entry, headword, wsFr, cache);
			entry.Comment.set_String(wsEn, cache.TsStrFactory.MakeString("Comment", wsEn));
			AddSenseToEntry(entry, gloss, wsEn, cache);
			return entry;
		}

		/// <summary>
		/// Creates an ILexEntry object, optionally with specified headword and gloss
		/// </summary>
		/// <param name="cache"></param>
		/// <param name="headword">Optional: defaults to 'Citation'</param>
		/// <param name="gloss">Optional: defaults to 'gloss'</param>
		/// <returns></returns>
		internal static ILexEntry CreateInterestingSuffix(FdoCache cache, string headword = "ba", string gloss = "gloss")
		{
			var entry = CreateInterestingLexEntry(cache, headword, gloss);
			var wsEn = cache.WritingSystemFactory.GetWsFromStr("en");
			var suffixType = cache.LangProject.LexDbOA.MorphTypesOA.FindOrCreatePossibility("suffix", wsEn);
			entry.LexemeFormOA = cache.ServiceLocator.GetInstance<IMoAffixAllomorphFactory>().Create();
			entry.LexemeFormOA.MorphTypeRA = suffixType as IMoMorphType;
			return entry;
		}

		internal sealed class TempGuidOn<T> : IDisposable where T : ICmObject
		{
			public T Item { get; private set; }
			private readonly Guid m_OriginalGuid;

			public TempGuidOn(T item, Guid tempGuid)
			{
				Item = item;
				m_OriginalGuid = item.Guid;
				SetGuidOn(item, tempGuid);
			}

			public void Dispose()
			{
				SetGuidOn(Item, m_OriginalGuid);
			}

			private static void SetGuidOn(ICmObject item, Guid newGuid)
			{
				var refGuidField = ReflectionHelper.GetField(item, "m_guid");
				ReflectionHelper.SetField(refGuidField, "m_guid", newGuid);
			}
		}

		/// <summary>
		/// Use reflection to set the guid on a variant form. May not work for all kinds of tests or appropriately be editing the database.
		/// Because changing the Guid causes teardown problem, it must be reset prior to teardown (hence the Disposable <returns/>)
		/// </summary>
		internal static TempGuidOn<ILexEntryRef> CreateVariantForm(FdoCache cache, IVariantComponentLexeme main, ILexEntry variantForm, Guid guid,
			string type = TestVariantName)
		{
			return new TempGuidOn<ILexEntryRef>(CreateVariantForm(cache, main, variantForm, type), guid);
		}

		/// <summary>
		/// 'internal static' so Reversal tests can use it
		/// </summary>
		internal static ILexEntryRef CreateVariantForm(FdoCache cache, IVariantComponentLexeme main, ILexEntry variantForm, string type = TestVariantName)
		{
			var owningList = cache.LangProject.LexDbOA.VariantEntryTypesOA;
			Assert.IsNotNull(owningList, "No VariantEntryTypes property on Lexicon object.");
			var varType = owningList.ReallyReallyAllPossibilities.LastOrDefault(poss => poss.Name.AnalysisDefaultWritingSystem.Text == type) as ILexEntryType;
			if (varType == null && type != null) // if this type doesn't exist, create it
			{
				varType = cache.ServiceLocator.GetInstance<ILexEntryTypeFactory>().Create();
				owningList.PossibilitiesOS.Add(varType);
				varType.Name.set_String(cache.DefaultAnalWs, type);
			}
			var refOut = variantForm.MakeVariantOf(main, varType);
			// ILexEntry.MakeVariantOf sets a Type even if null is specified. But we want to test typeless variants, so clear them if null is specified.
			if (type == null)
				refOut.VariantEntryTypesRS.Clear();
			return refOut;
		}

		/// <summary>
		/// Use reflection to set the guid on a complex form. May not work for all kinds of tests or appropriately be editing the database.
		/// Because changing the Guid causes teardown problem, it must be reset prior to teardown (hence the Disposable <returns/>)
		/// </summary>
		internal static TempGuidOn<ILexEntryRef> CreateComplexForm(FdoCache cache, IVariantComponentLexeme main, ILexEntry complexForm, Guid guid,
			bool subentry)
		{
			return new TempGuidOn<ILexEntryRef>(CreateComplexForm(cache, main, complexForm, subentry), guid);
		}

		internal static ILexEntryRef CreateComplexForm(FdoCache cache, ICmObject main, ILexEntry complexForm, bool subentry, byte complexFormTypeIndex = 1)
		{
			return CreateComplexForm(cache, main, complexForm, subentry,
				(ILexEntryType)cache.LangProject.LexDbOA.ComplexEntryTypesOA.PossibilitiesOS[complexFormTypeIndex]);
		}

		private static ILexEntryRef CreateComplexForm(FdoCache cache, ICmObject main, ILexEntry complexForm, bool subentry, Guid typeGuid)
		{
			return CreateComplexForm(cache, main, complexForm, subentry,
				(ILexEntryType)cache.LangProject.LexDbOA.ComplexEntryTypesOA.PossibilitiesOS.First(x => x.Guid == typeGuid));
		}

		private static ILexEntryRef CreateComplexForm(FdoCache cache, ICmObject main, ILexEntry complexForm, bool subentry, ILexEntryType complexEntryType)
		{
			var complexEntryRef = cache.ServiceLocator.GetInstance<ILexEntryRefFactory>().Create();
			complexForm.EntryRefsOS.Add(complexEntryRef);
			var complexEntryTypeAbbrText = complexEntryType.Abbreviation.BestAnalysisAlternative.Text;
			var complexEntryTypeRevAbbr = complexEntryType.ReverseAbbr;
			// If there is no reverseAbbr, generate one from the forward abbr (e.g. "comp. of") by trimming the trailing " of"
			if (complexEntryTypeRevAbbr.BestAnalysisAlternative.Equals(complexEntryTypeRevAbbr.NotFoundTss))
				complexEntryTypeRevAbbr.SetAnalysisDefaultWritingSystem(complexEntryTypeAbbrText.Substring(0, complexEntryTypeAbbrText.Length - 3));
			complexEntryRef.ComplexEntryTypesRS.Add(complexEntryType);
			complexEntryRef.RefType = LexEntryRefTags.krtComplexForm;
			complexEntryRef.ComponentLexemesRS.Add(main);
			if (subentry)
				complexEntryRef.PrimaryLexemesRS.Add(main);
			else
				complexEntryRef.ShowComplexFormsInRS.Add(main);
			return complexEntryRef;
		}

		/// <summary>
		/// Generates a Lexical Reference.
		/// If refTypeReverseName is specified, generates a Ref of an Asymmetric Type (EntryOrSenseTree) with the specified reverse name;
		/// otherwise, generates a Ref of a Symmetric Type (EntryOrSenseSequence).
		/// </summary>
		private void CreateLexicalReference(ICmObject mainEntry, ICmObject referencedForm, string refTypeName, string refTypeReverseName = null)
		{
			CreateLexicalReference(mainEntry, referencedForm, null, refTypeName, refTypeReverseName);
		}

		private void CreateLexicalReference(ICmObject firstEntry, ICmObject secondEntry, ICmObject thirdEntry, string refTypeName, string refTypeReverseName = null)
		{
			var lrt = Cache.ServiceLocator.GetInstance<ILexRefTypeFactory>().Create();
			if (Cache.LangProject.LexDbOA.ReferencesOA == null)
				Cache.LangProject.LexDbOA.ReferencesOA = Cache.ServiceLocator.GetInstance<ICmPossibilityListFactory>().Create();
			Cache.LangProject.LexDbOA.ReferencesOA.PossibilitiesOS.Add(lrt);
			lrt.Name.set_String(Cache.DefaultAnalWs, refTypeName);
			if (string.IsNullOrEmpty(refTypeReverseName))
			{
				lrt.MappingType = (int)MappingTypes.kmtEntryOrSenseSequence;
			}
			else
			{
				lrt.ReverseName.set_String(Cache.DefaultAnalWs, refTypeReverseName);
				lrt.MappingType = (int)MappingTypes.kmtEntryOrSenseTree;
			}
			var lexRef = Cache.ServiceLocator.GetInstance<ILexReferenceFactory>().Create();
			lrt.MembersOC.Add(lexRef);
			lexRef.TargetsRS.Add(firstEntry);
			lexRef.TargetsRS.Add(secondEntry);
			if (thirdEntry != null)
				lexRef.TargetsRS.Add(thirdEntry);
		}

		private ILexRefType CreateLexRefType(LexRefTypeTags.MappingTypes type, string name, string abbr, string revName, string revAbbr)
		{
			if (Cache.LangProject.LexDbOA.ReferencesOA == null)
				Cache.LangProject.LexDbOA.ReferencesOA = Cache.ServiceLocator.GetInstance<ICmPossibilityListFactory>().Create();
			var lrt = Cache.ServiceLocator.GetInstance<ILexRefTypeFactory>().Create();
			Cache.LangProject.LexDbOA.ReferencesOA.PossibilitiesOS.Add(lrt);
			lrt.MappingType = (int)type;
			lrt.Name.set_String(m_wsEn, name);
			lrt.Abbreviation.set_String(m_wsEn, abbr);
			if (!String.IsNullOrEmpty(revName))
				lrt.ReverseName.set_String(m_wsEn, revName);
			if (!String.IsNullOrEmpty(revAbbr))
				lrt.ReverseAbbreviation.set_String(m_wsEn, revAbbr);
			return lrt;
		}

		private void CreateLexReference(ILexRefType lrt, IEnumerable<ICmObject> sensesAndEntries)
		{
			CreateLexReference(lrt, sensesAndEntries, Guid.Empty);
		}

		private void CreateLexReference(ILexRefType lrt, IEnumerable<ICmObject> sensesAndEntries, Guid lexRefGuid)
		{
			var lexRef = Cache.ServiceLocator.GetInstance<ILexReferenceFactory>().Create(lexRefGuid, lrt);
			foreach (var senseOrEntry in sensesAndEntries)
				lexRef.TargetsRS.Add(senseOrEntry);
		}

		private ICmPossibility CreatePublicationType(string name)
		{
			if (Cache.LangProject.LexDbOA.PublicationTypesOA == null)
				Cache.LangProject.LexDbOA.PublicationTypesOA = Cache.ServiceLocator.GetInstance<ICmPossibilityListFactory>().Create();
			var item = Cache.ServiceLocator.GetInstance<ICmPossibilityFactory>().Create();
			Cache.LangProject.LexDbOA.PublicationTypesOA.PossibilitiesOS.Add(item);
			item.Name.set_String(m_wsEn, name);
			Cache.LangProject.LexDbOA.PublicationTypesOA.PossibilitiesOS.Add(item);
			return item;
		}

		private static void AddHeadwordToEntry(ILexEntry entry, string headword, int wsId, FdoCache cache)
		{
			// The headword field is special: it uses Citation if available, or LexemeForm if Citation isn't filled in
			entry.CitationForm.set_String(wsId, cache.TsStrFactory.MakeString(headword, wsId));
		}

		private static ILexPronunciation AddPronunciationToEntry(ILexEntry entry, string content, int wsId, FdoCache cache)
		{
			var pronunciation = cache.ServiceLocator.GetInstance<ILexPronunciationFactory>().Create();
			entry.PronunciationsOS.Add(pronunciation);
			pronunciation.Form.set_String(wsId, cache.TsStrFactory.MakeString(content, wsId));
			return pronunciation;
		}

		private static void AddSenseToEntry(ILexEntry entry, string gloss, int wsId, FdoCache cache)
		{
			var senseFactory = cache.ServiceLocator.GetInstance<ILexSenseFactory>();
			var sense = senseFactory.Create();
			entry.SensesOS.Add(sense);
			if (!string.IsNullOrEmpty(gloss))
				sense.Gloss.set_String(wsId, cache.TsStrFactory.MakeString(gloss, wsId));
		}

		private void AddSenseAndTwoSubsensesToEntry(ICmObject entryOrSense, string gloss)
		{
			var senseFactory = Cache.ServiceLocator.GetInstance<ILexSenseFactory>();
			var sense = senseFactory.Create();
			var entry = entryOrSense as ILexEntry;
			if (entry != null)
				entry.SensesOS.Add(sense);
			else
				((ILexSense)entryOrSense).SensesOS.Add(sense);
			sense.Gloss.set_String(m_wsEn, Cache.TsStrFactory.MakeString(gloss, m_wsEn));
			var subSensesOne = senseFactory.Create();
			sense.SensesOS.Add(subSensesOne);
			subSensesOne.Gloss.set_String(m_wsEn, Cache.TsStrFactory.MakeString(gloss + "2.1", m_wsEn));
			var subSensesTwo = senseFactory.Create();
			sense.SensesOS.Add(subSensesTwo);
			subSensesTwo.Gloss.set_String(m_wsEn, Cache.TsStrFactory.MakeString(gloss + "2.2", m_wsEn));
		}

		private void AddSingleSubSenseToSense(string gloss, ILexSense sense)
		{
			sense.Gloss.set_String(m_wsEn, Cache.TsStrFactory.MakeString(gloss, m_wsEn));
			AddSubSenseToSense(gloss + "1.1", sense);
		}

		private void AddSubSenseToSense(string gloss, ILexSense sense)
		{
			var subSensesOne = sense.Cache.ServiceLocator.GetInstance<ILexSenseFactory>().Create();
			sense.SensesOS.Add(subSensesOne);
			subSensesOne.Gloss.set_String(m_wsEn, Cache.TsStrFactory.MakeString(gloss, m_wsEn));
		}

		private ILexExampleSentence AddExampleToSense(ILexSense sense, string content, string translation = null)
		{
			var exampleFact = Cache.ServiceLocator.GetInstance<ILexExampleSentenceFactory>();
			var example = exampleFact.Create(new Guid(), sense);
			example.Example.set_String(m_wsFr, Cache.TsStrFactory.MakeString(content, m_wsFr));
			if (translation != null)
			{
				var type = Cache.ServiceLocator.GetInstance<ICmPossibilityRepository>().GetObject(CmPossibilityTags.kguidTranFreeTranslation);
				var cmTranslation = Cache.ServiceLocator.GetInstance<ICmTranslationFactory>().Create(example, type);
				cmTranslation.Translation.set_String(m_wsEn, Cache.TsStrFactory.MakeString(translation, m_wsEn));
				example.TranslationsOC.Add(cmTranslation);
			}
			return example;
		}

		private IMoForm AddAllomorphToEntry(ILexEntry entry)
		{
			var morphFact = Cache.ServiceLocator.GetInstance<IMoStemAllomorphFactory>();
			var morph = morphFact.Create();
			entry.AlternateFormsOS.Add(morph);
			morph.Form.set_String(m_wsFr, Cache.TsStrFactory.MakeString("Allomorph", m_wsFr));

			// add environment to the allomorph
			const int stringRepresentationFlid = 5097008;
			var env = Cache.ServiceLocator.GetInstance<IPhEnvironmentFactory>().Create();
			Cache.LangProject.PhonologicalDataOA.EnvironmentsOS.Add(env);
			morph.PhoneEnvRC.Add(env);
			Cache.MainCacheAccessor.SetString(env.Hvo, stringRepresentationFlid, Cache.TsStrFactory.MakeString("phoneyEnv", m_wsEn));

			return morph;
		}

		private static IStText CreateMultiParaText(string content, FdoCache cache)
		{
			var text = cache.ServiceLocator.GetInstance<ITextFactory>().Create();
			//cache.LangProject.
			var stText = cache.ServiceLocator.GetInstance<IStTextFactory>().Create();
			cache.LangProject.InterlinearTexts.Add(stText);
			text.ContentsOA = stText;
			var para = cache.ServiceLocator.GetInstance<IStTxtParaFactory>().Create();
			stText.ParagraphsOS.Add(para);
			para.Contents = MakeVernTss("First para " + content, cache);
			var para1 = cache.ServiceLocator.GetInstance<IStTxtParaFactory>().Create();
			stText.ParagraphsOS.Add(para1);
			para1.Contents = MakeVernTss("Second para " + content, cache);
			return text.ContentsOA;
		}

		private static ITsString MakeVernTss(string content, FdoCache cache)
		{
			return cache.TsStrFactory.MakeString(content, cache.DefaultVernWs);
		}

		private ITsString MakeMulitlingualTss(IEnumerable<string> content)
		{
			// automatically alternates runs between 'en' and 'fr'
			var tsFact = Cache.TsStrFactory;
			var lastWs = m_wsFr;
			var builder = tsFact.GetIncBldr();
			foreach (var runContent in content)
			{
				lastWs = lastWs == m_wsEn ? m_wsFr : m_wsEn; // switch ws for each run
				builder.AppendTsString(tsFact.MakeString(runContent, lastWs));
			}
			return builder.GetString();
		}

		private ITsString MakeBidirectionalTss(IEnumerable<string> content)
		{
			EnsureHebrewExists();
			// automatically alternates runs between 'en' and 'he' (Hebrew)
			var tsFact = Cache.TsStrFactory;
			var lastWs = m_wsEn;
			var builder = tsFact.GetIncBldr();
			foreach (var runContent in content)
			{
				lastWs = lastWs == m_wsEn ? m_wsHe : m_wsEn; // switch ws for each run
				builder.AppendTsString(tsFact.MakeString(runContent, lastWs));
			}
			return builder.GetString();
		}

		private void EnsureHebrewExists()
		{
			if (m_wsHe > 0)
				return;
			var wsManager = Cache.ServiceLocator.WritingSystemManager;
			IWritingSystem hebrew;
			wsManager.GetOrSet("he", out hebrew);
			hebrew.RightToLeftScript = true;
			m_wsHe = hebrew.Handle;
		}

		private void SetDictionaryNormalDirection(InheritableStyleProp<TriStateBool> rightToLeft)
		{
			ReflectionHelper.SetField(DictionaryNormalStyle, "m_rtl", rightToLeft);
		}

		internal static void SetPublishAsMinorEntry(ILexEntry entry, bool publish)
		{
			foreach (var ler in entry.EntryRefsOS)
				ler.HideMinorEntry = publish ? 0 : 1;
		}

		public static DictionaryNodeOptions GetWsOptionsForLanguages(string[] languages)
		{
			return new DictionaryNodeWritingSystemOptions { Options = DictionaryDetailsControllerTests.ListOfEnabledDNOsFromStrings(languages) };
		}

		public static DictionaryNodeOptions GetWsOptionsForLanguages(string[] languages, DictionaryNodeWritingSystemOptions.WritingSystemType type)
		{
			return new DictionaryNodeWritingSystemOptions
			{
				Options = DictionaryDetailsControllerTests.ListOfEnabledDNOsFromStrings(languages),
				WsType = type
			};
		}

		public static DictionaryNodeOptions GetWsOptionsForLanguageswithDisplayWsAbbrev(string[] languages)
		{
			return new DictionaryNodeWritingSystemOptions
			{
				Options = DictionaryDetailsControllerTests.ListOfEnabledDNOsFromStrings(languages),
				DisplayWritingSystemAbbreviations = true
			};
		}

		public static DictionaryNodeOptions GetListOptionsForItems(DictionaryNodeListOptions.ListIds listName, ICmPossibility[] checkedItems)
		{
			var listOptions = new DictionaryNodeListOptions
			{
				ListId = listName,
				Options = DictionaryDetailsControllerTests.ListOfEnabledDNOsFromStrings(checkedItems.Select(id => id.Guid.ToString()).ToList())
			};
			return listOptions;
		}

		public static DictionaryNodeOptions GetListOptionsForStrings(DictionaryNodeListOptions.ListIds listName, IEnumerable<string> checkedItems)
		{
			var listOptions = new DictionaryNodeListOptions
			{
				ListId = listName,
				Options = DictionaryDetailsControllerTests.ListOfEnabledDNOsFromStrings(checkedItems)
			};
			return listOptions;
		}

		public DictionaryNodeOptions GetFullyEnabledListOptions(DictionaryNodeListOptions.ListIds listName)
		{
			return GetFullyEnabledListOptions(Cache, listName);
		}

		public static DictionaryNodeOptions GetFullyEnabledListOptions(FdoCache cache, DictionaryNodeListOptions.ListIds listName)
		{
			List<DictionaryNodeListOptions.DictionaryNodeOption> dnoList;
			var useParaOptions = false;
			switch (listName)
			{
				case DictionaryNodeListOptions.ListIds.Minor:
					dnoList = DictionaryDetailsControllerTests.ListOfEnabledDNOsFromStrings(
						new[] { XmlViewsUtils.GetGuidForUnspecifiedVariantType(), XmlViewsUtils.GetGuidForUnspecifiedComplexFormType() }
							.Select(guid => guid.ToString())
						.Union(cache.LangProject.LexDbOA.ComplexEntryTypesOA.PossibilitiesOS
						.Union(cache.LangProject.LexDbOA.VariantEntryTypesOA.PossibilitiesOS).Select(item => item.Guid.ToString())));
					break;
				case DictionaryNodeListOptions.ListIds.Variant:
					dnoList = DictionaryDetailsControllerTests.ListOfEnabledDNOsFromStrings(
						new[] { XmlViewsUtils.GetGuidForUnspecifiedVariantType().ToString() }
						.Union(cache.LangProject.LexDbOA.VariantEntryTypesOA.PossibilitiesOS.Select(item => item.Guid.ToString())));
					break;
				case DictionaryNodeListOptions.ListIds.Complex:
					useParaOptions = true;
					dnoList = DictionaryDetailsControllerTests.ListOfEnabledDNOsFromStrings(
						new[] { XmlViewsUtils.GetGuidForUnspecifiedComplexFormType().ToString() }
						.Union(cache.LangProject.LexDbOA.ComplexEntryTypesOA.PossibilitiesOS.Select(item => item.Guid.ToString())));
					break;
				case DictionaryNodeListOptions.ListIds.Note:
					useParaOptions = true;
					dnoList = DictionaryDetailsControllerTests.ListOfEnabledDNOsFromStrings(
						new[] { XmlViewsUtils.GetGuidForUnspecifiedExtendedNoteType().ToString() }
						.Union(cache.LangProject.LexDbOA.ExtendedNoteTypesOA.PossibilitiesOS.Select(item => item.Guid.ToString())));
					break;
				default:
					throw new NotImplementedException(string.Format("Unknown list id {0}", listName));
			}

			DictionaryNodeListOptions listOptions = useParaOptions ? new DictionaryNodeListAndParaOptions() : new DictionaryNodeListOptions();

			listOptions.ListId = listName;
			listOptions.Options = dnoList;
			return listOptions;
		}

		/// <summary>
		/// Search haystack with regexQuery, and assert that requiredNumberOfMatches matches are found.
		/// Can be used in place of AssertThatXmlIn.String().HasSpecifiedNumberOfMatchesForXpath(),
		/// when slashes are needed in an argument to xpath starts-with.
		/// </summary>
		private static void AssertRegex(string haystack, string regexQuery, int requiredNumberOfMatches)
		{
			var regex = new Regex(regexQuery);
			var matches = regex.Matches(haystack);
			Assert.That(matches.Count, Is.EqualTo(requiredNumberOfMatches), "Unexpected number of matches");
		}

		public IPartOfSpeech CreatePartOfSpeech(string name, string abbr)
		{
			var posSeq = Cache.LangProject.PartsOfSpeechOA.PossibilitiesOS;
			var pos = Cache.ServiceLocator.GetInstance<IPartOfSpeechFactory>().Create();
			posSeq.Add(pos);
			pos.Name.set_String(m_wsEn, name);
			pos.Abbreviation.set_String(m_wsEn, abbr);
			return pos;
		}

		// ReSharper disable once InconsistentNaming
		public IMoMorphSynAnalysis CreateMSA(ILexEntry entry, IPartOfSpeech pos)
		{
			var msa = Cache.ServiceLocator.GetInstance<IMoStemMsaFactory>().Create();
			entry.MorphoSyntaxAnalysesOC.Add(msa);
			msa.PartOfSpeechRA = pos;
			return msa;
		}
	}

	#region Test classes and interfaces for testing the reflection code in GetPropertyTypeForConfigurationNode
	internal class TestRootClass
	{
		public ITestInterface RootMember { get; set; }
		public TestNonInterface ConcreteMember { get; set; }
	}

	internal interface ITestInterface : ITestBaseOne, ITestBaseTwo
	{
		string TestString { get; }
	}

	internal interface ITestBaseOne
	{
		IMoForm TestMoForm { get; }
	}

	internal interface ITestBaseTwo : ITestGrandParent
	{
		ICmObject TestIcmObject { get; }
	}

	internal class TestNonInterface
	{
		// ReSharper disable UnusedMember.Local // Justification: called by reflection
		private string TestNonInterfaceString { get; set; }
		// ReSharper restore UnusedMember.Local
	}

	internal interface ITestGrandParent
	{
		Stack<TestRootClass> TestCollection { get; }
	}

	internal class TestPictureClass
	{
		public IFdoList<ICmPicture> Pictures { get; set; }
	}
	#endregion
}
