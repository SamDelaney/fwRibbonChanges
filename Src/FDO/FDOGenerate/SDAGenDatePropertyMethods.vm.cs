﻿## --------------------------------------------------------------------------------------------
## Copyright (C) 2007-2008 SIL International. All rights reserved.
##
## Distributable under the terms of either the Common Public License or the
## GNU Lesser General Public License, as specified in the LICENSING.txt file.
##
## NVelocity template file
## This file is used by the FdoGenerate task to generate the source code from the XMI
## database model.
## --------------------------------------------------------------------------------------------
## This will generate the various methods used by the FDO aware ISilDataAccess implementation.
## Using these methods avoids using Reflection in that implementation.
##
		/// <summary>
		/// Get a GenDate type property.
		/// </summary>
		/// <param name="flid">The property to read.</param>
		protected override GenDate GetGenDatePropertyInternal(int flid)
		{
			switch (flid)
			{
				default:
					return base.GetGenDatePropertyInternal(flid);
## Loop over each property and do something with the ones that return GenDates.
#foreach( $prop in $class.GenDateProperties )
				case $prop.Number:
#if( $prop.IsHandGenerated)
					return $prop.NiuginianPropName;
#else
					return m_$prop.NiuginianPropName;
#end
#end
			}
		}

		/// <summary>
		/// Set a GenDate type property.
		/// </summary>
		/// <param name="flid">The property to read.</param>
		/// <param name="newValue">The new property value.</param>
		/// <param name="useAccessor"></param>
		protected override void SetPropertyInternal(int flid, GenDate newValue, bool useAccessor)
		{
			switch (flid)
			{
				default:
					base.SetPropertyInternal(flid, newValue, useAccessor);
					break;
## Loop over each property and do something with the ones that are GenDates.
#foreach( $prop in $class.GenDateProperties )
				case $prop.Number:
#if( $prop.OverridenType == "")
					if (useAccessor)
						$prop.NiuginianPropName = newValue;
					else
						m_$prop.NiuginianPropName = newValue;
#else
					if (useAccessor)
						$prop.NiuginianPropName = ($prop.OverridenType)newValue;
					else
						m_$prop.NiuginianPropName = newValue;
#end
					break;
#end
			}
		}