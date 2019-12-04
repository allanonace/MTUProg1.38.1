﻿using Library;
using Library.Exceptions;
using System;
using System.Threading.Tasks;

using RegType       = MTUComm.MemoryMap.MemoryMap.RegType;
using REGISTER_TYPE = MTUComm.MemoryMap.AMemoryMap.REGISTER_TYPE;

namespace MTUComm.MemoryMap
{
    public class MemoryOverload<T> : IEquatable<MemoryOverload<T>>
    {
        #region Constants

        private enum CUSTOM_TYPE { OPERATION, METHOD }

        #endregion

        #region Attributes

        public Func<Task<T>> funcGet;
        public string id { get; }
        public string description { get; }
        public RegType valueType { get; }
        public string[] registerIds { get; }
        public string custom_Get { get; }
        public string methodId { get; }
        private CUSTOM_TYPE customType;
        public REGISTER_TYPE registerType { get; }

        #endregion

        #region Properties

        private bool _HasCustomMethod
        {
            get { return this.custom_Get.ToLower ().StartsWith ( MemoryMap.METHOD ); }
        }

        private bool _HasCustomMethodId
        {
            get { return this.custom_Get.ToLower ().StartsWith (MemoryMap.METHOD_KEY ); }
        }

        private bool _HasCustomOperation
        {
            get { return ! this._HasCustomMethod      &&
                           this.valueType < RegType.CHAR &&
                         ! string.IsNullOrEmpty ( this.custom_Get ); }
        }

        public bool HasCustomMethod
        {
            get { return this.customType == CUSTOM_TYPE.METHOD; }
        }

        public bool HasCustomOperation
        {
            get { return this.customType == CUSTOM_TYPE.OPERATION; }
        }

        public async Task<T> GetValue ()
        {
            return await this.funcGet ();
        }

        #endregion

        #region Initialization

        public MemoryOverload (
            string id,
            RegType type,
            string description,
            string[] registerIds,
            string custom )
        {
            this.id           = id;
            this.valueType    = type;
            this.description  = description;
            this.registerIds  = registerIds;
            this.custom_Get       = custom.Replace ( " ", string.Empty );
            this.registerType = REGISTER_TYPE.OVERLOAD;

            if      ( this._HasCustomMethod    ) this.customType = CUSTOM_TYPE.METHOD;
            else if ( this._HasCustomOperation ) this.customType = CUSTOM_TYPE.OPERATION;
            else
            {
                // Selected dynamic member not exists
                Utils.Print ( "Get " + id + ": Error - Overload registers need custom field" );

                if ( ! MemoryMap.isUnityTest )
                    throw new OverloadEmptyCustomException ( id );
            }

            if ( this.HasCustomMethod )
            {
                if ( this._HasCustomMethodId )
                     this.methodId = this.custom_Get.Substring ( MemoryMap.METHOD_KEY.Length );
                else this.methodId = this.id + MemoryMap.METHOD_SUFIX_GET;
            }
        }

        #endregion

        #region Compare

        public bool Equals ( MemoryOverload<T> other )
        {
            if ( other == null )
                return false;

            if ( this.registerIds.Length == other.registerIds.Length )
                for ( int i = this.registerIds.Length - 1; i >= 0; i-- )
                    if ( ! string.Equals ( this.registerIds[ i ], other.registerIds[ i ] ) )
                        return false;

            bool ok_id          = string.Equals ( this.id, other.id );
            bool ok_description = string.Equals ( this.description, other.description );
            bool ok_methodId    = string.Equals ( this.methodId, other.methodId );

            return ok_id          &&
                   ok_description &&
                   ok_methodId;
        }

        #endregion
    }
}
