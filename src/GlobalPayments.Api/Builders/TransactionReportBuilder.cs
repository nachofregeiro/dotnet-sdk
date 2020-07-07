﻿using System;
using GlobalPayments.Api.Entities;

namespace GlobalPayments.Api.Builders {
    public class TransactionReportBuilder<TResult> : ReportBuilder<TResult> where TResult : class {
        internal string DeviceId {
            get {
                return SearchBuilder.UniqueDeviceId;
            }
        }
        internal DateTime? EndDate {
            get {
                return SearchBuilder.EndDate;
            }
        }
        internal DateTime? StartDate {
            get {
                return SearchBuilder.StartDate;
            }
        }
        internal string TransactionId { get; set; }
        internal int? Page { get; set; }
        internal int? PageSize { get; set; }
        internal TransactionSortProperty? OrderProperty { get; set; }
        internal SortDirection? OrderDirection { get; set; }


        private SearchCriteriaBuilder<TResult> _searchBuilder;
        internal SearchCriteriaBuilder<TResult> SearchBuilder {
            get {
                if (_searchBuilder == null)
                    _searchBuilder = new SearchCriteriaBuilder<TResult>(this);
                return _searchBuilder;
            }
        }

        /// <summary>
        /// Sets the device ID as criteria for the report.
        /// </summary>
        /// <param name="value">The device ID</param>
        /// <returns>TResult</returns>
        public TransactionReportBuilder<TResult> WithDeviceId(string value) {
            SearchBuilder.UniqueDeviceId = value;
            return this;
        }

        /// <summary>
        /// Sets the end date ID as criteria for the report.
        /// </summary>
        /// <param name="value">The end date ID</param>
        /// <returns>TResult</returns>
        public TransactionReportBuilder<TResult> WithEndDate(DateTime? value) {
            SearchBuilder.EndDate = value;
            return this;
        }

        /// <summary>
        /// Sets the start date ID as criteria for the report.
        /// </summary>
        /// <param name="value">The start date ID</param>
        /// <returns>TResult</returns>
        public TransactionReportBuilder<TResult> WithStartDate(DateTime? value) {
            SearchBuilder.StartDate = value;
            return this;
        }

        public TransactionReportBuilder<TResult> WithTimeZoneConversion(TimeZoneConversion value) {
            TimeZoneConversion = value;
            return this;
        }

        /// <summary>
        /// Sets the gateway transaction ID as criteria for the report.
        /// </summary>
        /// <param name="value">The gateway transaction ID</param>
        /// <returns>TResult</returns>
        public TransactionReportBuilder<TResult> WithTransactionId(string value) {
            TransactionId = value;
            return this;
        }

        /// <summary>
        /// Set the gateway paging criteria for the report.
        /// </summary>
        /// <param name="page">Page number</param>
        /// <param name="pageSize">Page size</param>
        /// <returns>TResult</returns>
        public TransactionReportBuilder<TResult> WithPaging(int page, int pageSize)
        {
            Page = page;
            PageSize = pageSize;
            return this;
        }

        /// <summary>
        /// Set the gateway order by criteria for the report.
        /// </summary>
        /// <param name="orderProperty">Order by property</param>
        /// <param name="orderDirection">Order by direction</param>
        /// <returns>TResult</returns>
        public TransactionReportBuilder<TResult> OrderBy(TransactionSortProperty orderProperty, SortDirection orderDirection = SortDirection.Ascending)
        {
            OrderProperty = orderProperty;
            OrderDirection = orderDirection;
            return this;
        }

        public SearchCriteriaBuilder<TResult> Where<T>(SearchCriteria criteria, T value) {
            return SearchBuilder.And(criteria, value);
        }

        public SearchCriteriaBuilder<TResult> Where<T>(DataServiceCriteria criteria, T value) {
            return SearchBuilder.And(criteria, value);
        }

        public TransactionReportBuilder(ReportType type) : base(type) { }

        protected override void SetupValidations() {
            Validations.For(ReportType.TransactionDetail)
                .Check(() => TransactionId).IsNotNull();

            Validations.For(ReportType.Activity).Check(() => TransactionId).IsNull();
        }
    }
}
