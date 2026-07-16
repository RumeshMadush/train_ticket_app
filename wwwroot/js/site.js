(() => {
  const body = document.body;
  document.querySelectorAll('[data-sidebar-open]').forEach(button => button.addEventListener('click', () => body.classList.add('sidebar-open')));
  document.querySelectorAll('[data-sidebar-close]').forEach(button => button.addEventListener('click', () => body.classList.remove('sidebar-open')));
  document.querySelectorAll('[data-alert-close]').forEach(button => button.addEventListener('click', () => button.closest('.app-alert')?.remove()));

  const bookingTypeInputs = document.querySelectorAll('input[name="BookingType"]');
  const recurrencePanel = document.querySelector('[data-recurrence-panel]');
  const frequencySelect = document.querySelector('[name="RecurrenceFrequency"]');
  const weeklyDays = document.querySelector('[data-weekly-days]');
  const updateRecurrence = () => {
    const selected = document.querySelector('input[name="BookingType"]:checked');
    const isRecurring = selected?.value === 'Recurring' || selected?.value === '1';
    if (recurrencePanel) recurrencePanel.hidden = !isRecurring;
    if (weeklyDays && frequencySelect) weeklyDays.hidden = frequencySelect.value !== 'Weekly' && frequencySelect.value !== '1';
  };
  bookingTypeInputs.forEach(input => input.addEventListener('change', updateRecurrence));
  frequencySelect?.addEventListener('change', updateRecurrence);
  updateRecurrence();

  const scheduleSelect = document.querySelector('[data-schedule-select]');
  const travelDate = document.querySelector('[data-travel-date]');
  const reservationList = document.querySelector('[data-reservation-list]');
  const reservationTemplate = document.querySelector('[data-reservation-template]');
  let seatOptions = [];

  const updateTotal = () => {
    const total = [...document.querySelectorAll('[data-reserved-price]')]
      .reduce((sum, input) => sum + (Number.parseFloat(input.value) || 0), 0);
    const output = document.querySelector('[data-booking-total]');
    if (output) output.textContent = total.toFixed(2);
  };

  const fillSeatSelect = (select, selectedValue) => {
    if (!select) return;
    select.innerHTML = '<option value="0">Choose seat</option>' + seatOptions.map(seat =>
      `<option value="${seat.id}" data-price="${seat.price}" ${String(seat.id) === String(selectedValue) ? 'selected' : ''} ${!seat.isAvailable && String(seat.id) !== String(selectedValue) ? 'disabled' : ''}>${seat.seatNumber} · ${seat.seatClass}${seat.isAvailable ? '' : ' · reserved on first date'}</option>`
    ).join('');
  };

  const loadSeats = async () => {
    if (!scheduleSelect || !travelDate || !scheduleSelect.value || scheduleSelect.value === '0' || !travelDate.value) return;
    const excluded = document.querySelector('[data-exclude-booking]')?.value;
    const url = `/Booking/SeatOptions?scheduleId=${encodeURIComponent(scheduleSelect.value)}&travelDate=${encodeURIComponent(travelDate.value)}${excluded ? `&excludeBookingId=${encodeURIComponent(excluded)}` : ''}`;
    const response = await fetch(url, { headers: { Accept: 'application/json' } });
    if (!response.ok) return;
    seatOptions = await response.json();
    document.querySelectorAll('[data-seat-select]').forEach(select => fillSeatSelect(select, select.value));
  };

  const reindexRows = () => {
    document.querySelectorAll('[data-reservation-row]').forEach((row, index) => {
      const number = row.querySelector('.reservation-number'); if (number) number.textContent = String(index + 1);
      row.querySelectorAll('[name]').forEach(input => input.name = input.name.replace(/SeatReservations\[\d+\]/, `SeatReservations[${index}]`));
    });
    updateTotal();
  };

  document.addEventListener('click', event => {
    const add = event.target.closest('[data-add-reservation]');
    if (add && reservationList && reservationTemplate) {
      const index = reservationList.querySelectorAll('[data-reservation-row]').length;
      reservationList.insertAdjacentHTML('beforeend', reservationTemplate.innerHTML.replaceAll('__index__', index).replaceAll('__number__', index + 1));
      fillSeatSelect(reservationList.querySelector('[data-reservation-row]:last-child [data-seat-select]'), 0);
      reindexRows();
    }
    const remove = event.target.closest('[data-remove-reservation]');
    if (remove && document.querySelectorAll('[data-reservation-row]').length > 1) { remove.closest('[data-reservation-row]')?.remove(); reindexRows(); }
  });
  document.addEventListener('change', event => {
    const select = event.target.closest('[data-seat-select]');
    if (select) {
      const row = select.closest('[data-reservation-row]');
      const price = row?.querySelector('[data-reserved-price]');
      const option = select.options[select.selectedIndex];
      if (price && option?.dataset.price) price.value = Number.parseFloat(option.dataset.price).toFixed(2);
      updateTotal();
    }
  });
  document.addEventListener('input', event => { if (event.target.matches('[data-reserved-price]')) updateTotal(); });
  scheduleSelect?.addEventListener('change', loadSeats);
  travelDate?.addEventListener('change', loadSeats);
  loadSeats();
  updateTotal();

  document.querySelectorAll('[data-suggestion]').forEach(button => button.addEventListener('click', () => {
    const input = document.querySelector('[data-chat-input]');
    if (input) { input.value = button.dataset.suggestion || ''; input.focus(); }
  }));
  const messages = document.querySelector('[data-chat-messages]');
  if (messages) messages.scrollTop = messages.scrollHeight;
})();
