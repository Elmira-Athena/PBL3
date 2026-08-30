using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using PBL3.Core.Entities;
using PBL3.Core.Interfaces;
using PBL3.Shared.DTOs.ServiceTickets;

namespace PBL3.Service.ServiceTickets
{
    public class WarrantyEvaluator
    {
        public static async Task<WarrantyEvaluation> EvaluateAsync(
            ProductSerial serial,
            ProductVariant variant,
            IWarrantyRepository warrantyRepository)
        {
            var now = DateTime.UtcNow;

            // 1. Try to find active Warranty row
            var activeWarranties = await warrantyRepository.GetActiveBySerialIdReadOnlyAsync(serial.Id);
            if (activeWarranties.Count > 0)
            {
                var warranty = activeWarranties[0]; // Already ordered by EndDate desc
                return new WarrantyEvaluation(
                    IsInWarranty: warranty.EndDate > now,
                    ExpiresOn: warranty.EndDate,
                    Source: 0, // WarrantyRow
                    WarrantyId: warranty.Id
                );
            }

            // 2. Fallback: Compute from SoldDate + WarrantyMonth
            if (serial.SoldDate.HasValue && variant.WarrantyMonth > 0)
            {
                var expiresOn = serial.SoldDate.Value.AddMonths(variant.WarrantyMonth);
                return new WarrantyEvaluation(
                    IsInWarranty: expiresOn > now,
                    ExpiresOn: expiresOn,
                    Source: 1, // ComputedFromSoldDate
                    WarrantyId: null
                );
            }

            // 3. No warranty
            return new WarrantyEvaluation(
                IsInWarranty: false,
                ExpiresOn: null,
                Source: 2, // NoWarranty
                WarrantyId: null
            );
        }
    }

    public record WarrantyEvaluation(bool IsInWarranty, DateTime? ExpiresOn, byte Source, int? WarrantyId);
}
