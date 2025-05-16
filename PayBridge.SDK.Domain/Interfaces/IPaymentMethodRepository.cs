using PayBridge.SDK.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PayBridge.SDK.Domain.Interfaces;
public interface IPaymentMethodRepository
{ 
    /// <summary>
  /// Creates a new payment method in the repository
  /// </summary>
    Task<PaymentMethod> CreateAsync(PaymentMethod paymentMethod);

    /// <summary>
    /// Gets a payment method by its ID
    /// </summary>
    Task<PaymentMethod> GetByIdAsync(string id);

    /// <summary>
    /// Gets payment methods for a specific customer
    /// </summary>
    Task<IEnumerable<PaymentMethod>> GetByCustomerEmailAsync(string email);

    /// <summary>
    /// Gets the default payment method for a customer
    /// </summary>
    Task<PaymentMethod> GetDefaultForCustomerAsync(string email);

    /// <summary>
    /// Updates an existing payment method
    /// </summary>
    Task<PaymentMethod> UpdateAsync(PaymentMethod paymentMethod);

    /// <summary>
    /// Deletes a payment method
    /// </summary>
    Task DeleteAsync(string id);
}
