<?xml version="1.0" encoding="utf-8"?>
<xsl:stylesheet xmlns:xsl="http://www.w3.org/1999/XSL/Transform" version="1.0">
	<xsl:output method="xml" encoding="UTF-8" indent="yes"/>
	<!-- This stylesheet transforms an interlinear text as output by FieldWorks into a form which conforms to the XLingPap DTD and can then be edited with the XMLmind XML Editor usingthe XLingPap configuration.  -->
	<!--
		================================================================
		FieldWorks Language Explorer interlinear XML to XLingPap mapper for Stage 1.
		Input:    XML output of FLEx interlinear, where result concatenates morphemes within a word
		Output: XLingPap XML

		================================================================
		Revision History is at the end of this file.

		- - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
		Preamble
		- - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
	-->
	<xsl:key name="Language" match="language" use="@lang"/>
	<xsl:param name="sHyphen" select="'-'"/>
	<!--
		- - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
		Main template
		- - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
	-->
	<xsl:template match="document">
		<!-- output dtd path -->
		<xsl:text disable-output-escaping="yes">&#xa;&#x3c;!DOCTYPE lingPaper PUBLIC   "-//XMLmind//DTD XLingPap//EN" "XLingPap.dtd"&#x3e;&#xa;</xsl:text>
		<lingPaper>
			<frontMatter>
				<title>
					<xsl:choose>
						<xsl:when test="//interlinear-text/item[@type='title']">
							<xsl:value-of select="//interlinear-text/item[@type='title']"/>
						</xsl:when>
						<xsl:otherwise>
							<xsl:text>FieldWorks Language Explorer Interlinear Export</xsl:text>
						</xsl:otherwise>
					</xsl:choose>
				</title>
				<author>
					<xsl:choose>
						<xsl:when test="//interlinear-text/item[@type='source']">
							<xsl:value-of select="//interlinear-text/item[@type='source']"/>
						</xsl:when>
						<xsl:otherwise>
							<xsl:text>[Insert author's name here]</xsl:text>
						</xsl:otherwise>
					</xsl:choose>
				</author>
			</frontMatter>
			<section1 id="s1">
				<secTitle>First Section</secTitle>
				<p/>
			</section1>
			<backMatter>
				<appendix id="a1">
					<secTitle>Interlinear text</secTitle>
					<interlinear-text>
						<textInfo>
							<textTitle>
								<xsl:value-of select="//interlinear-text/item[@type='title']"/>
							</textTitle>
							<shortTitle>
								<xsl:value-of select="//interlinear-text/item[@type='title-abbreviation']"/>
							</shortTitle>
						</textInfo>
						<xsl:apply-templates/>
					</interlinear-text>
				</appendix>
				<endnotes/>
				<references/>
			</backMatter>
			<languages>
				<xsl:for-each select="//language">
					<xsl:variable name="sLangId" select="@lang"/>
					<xsl:if test="//item[@lang=$sLangId]">
						<language id="{@lang}" font-family="{@font}">
							<xsl:if test="@vernacular='true'">
								<xsl:attribute name="name">vernacular</xsl:attribute>
							</xsl:if>
						</language>
					</xsl:if>
				</xsl:for-each>
			</languages>
			<types>
				<xsl:call-template name="CommonTypes"/>
				<type id="tGrammaticalGloss" font-variant="small-caps"/>
				<type id="tWordPos"/>
				<type id="tLiteralTranslation"/>
			</types>
		</lingPaper>
	</xsl:template>
	<!--
		- - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
		phrase
		- - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
	-->
	<xsl:template match="phrase">
		<xsl:variable name="sLevel">
			<xsl:if test="item[@type='segnum']">
				<xsl:value-of select="item[@type='segnum']"/>
			</xsl:if>
		</xsl:variable>
		<xsl:variable name="sThisTextId">
			<!-- we really need something from the DB or some such.  Am trying this in hopes it will be unique in most cases -->
			<xsl:for-each select="//phrase">
				<xsl:if test="position()=last()">
					<xsl:value-of select="generate-id()"/>
				</xsl:if>
			</xsl:for-each>
		</xsl:variable>
		<interlinear text="T-{$sThisTextId}-{$sLevel}" textref="T-{$sThisTextId}-{$sLevel}">
			<xsl:call-template name="OutputInterlinearContent"/>
		</interlinear>
	</xsl:template>
	<xsl:include href="xml2XLingPapCommonConcatMorphemes.xsl"/>
</xsl:stylesheet>