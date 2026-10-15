(() => {
  const toastHost = document.getElementById('customerToastHost');
  const money = value => new Intl.NumberFormat('vi-VN').format(Math.round(value)) + ' đ';
  const updateCartCount = count => {
    const badge = document.getElementById('navCartCount');
    if (badge) badge.textContent = count;
    const menuCount = document.getElementById('menuCartCount');
    if (menuCount) menuCount.textContent = `(${count})`;
  };
  const showToast = message => {
    if (!toastHost) return;
    const toast = document.createElement('div');
    toast.className = 'customer-toast';
    toast.setAttribute('role', 'status');
    toast.innerHTML = '<span class="customer-toast-check">✓</span><span></span><button type="button" aria-label="Đóng thông báo">×</button>';
    toast.querySelector('span:nth-child(2)').textContent = message;
    toast.querySelector('button').addEventListener('click', () => toast.remove());
    toastHost.append(toast);
    window.setTimeout(() => toast.remove(), 3600);
  };
  const sendForm = async form => {
    const data = new FormData(form);
    return fetch(form.action, {
      method: 'POST', body: data,
      headers: { 'X-Requested-With': 'XMLHttpRequest' }
    }).then(async response => {
      if (!response.ok) throw new Error('Yêu cầu chưa hoàn tất. Vui lòng thử lại.');
      return response.json();
    });
  };

  document.querySelectorAll('.add-to-cart-form').forEach(form => {
    form.addEventListener('submit', async event => {
      event.preventDefault();
      const button = form.querySelector('.add-to-cart-btn');
      if (!button || button.disabled) return;
      const original = button.innerHTML;
      button.disabled = true;
      button.classList.add('is-loading');
      button.innerHTML = '<span>…</span><span>Đang thêm</span>';
      try {
        const result = await sendForm(form);
        if (!result.success) throw new Error(result.message || 'Không thể thêm món vào giỏ.');
        updateCartCount(result.count);
        showToast(result.message || 'Đã thêm món vào giỏ hàng.');
        button.classList.add('is-added');
        button.innerHTML = '<span>✓</span><span>Đã thêm</span>';
        window.setTimeout(() => { button.innerHTML = original; button.classList.remove('is-added'); }, 1400);
      } catch (error) {
        showToast(error.message || 'Không thể thêm món vào giỏ.');
        button.innerHTML = original;
      } finally {
        button.disabled = false;
        button.classList.remove('is-loading');
      }
    });
  });

  document.querySelectorAll('.cart-quantity-form').forEach(form => {
    const input = form.querySelector('.cart-quantity-input');
    if (!input) return;
    let timer;
    let lastValue = input.value;
    const update = async () => {
      input.value = Math.min(100, Math.max(1, Number(input.value || 1)));
      if (input.value === lastValue) return;
      const row = form.closest('[data-cart-row]');
      form.classList.add('is-updating');
      try {
        const result = await sendForm(form);
        input.value = result.quantity;
        lastValue = input.value;
        row.querySelector('.cart-line-total').textContent = money(result.lineTotal);
        document.getElementById('cartSubtotal').textContent = money(result.total);
        document.getElementById('cartGrandTotal').textContent = money(result.total);
        updateCartCount(result.count);
        const subtitle = document.querySelector('.cart-summary-row span');
        if (subtitle) subtitle.textContent = `Tạm tính (${result.count} món)`;
      } catch (error) {
        input.value = lastValue;
        showToast(error.message || 'Chưa cập nhật được số lượng.');
      } finally {
        form.classList.remove('is-updating');
      }
    };
    input.addEventListener('change', update);
    input.addEventListener('keydown', event => { if (event.key === 'Enter') { event.preventDefault(); update(); } });
    form.querySelectorAll('[data-cart-delta]').forEach(button => button.addEventListener('click', () => {
      input.value = Math.min(100, Math.max(1, Number(input.value || 1) + Number(button.dataset.cartDelta)));
      update();
    }));
  });

  document.querySelectorAll('.cart-remove-form').forEach(form => form.addEventListener('submit', async event => {
    event.preventDefault();
    const row = form.closest('[data-cart-row]');
    const button = form.querySelector('button');
    button.disabled = true;
    try {
      const result = await sendForm(form);
      row.classList.add('is-removing');
      window.setTimeout(() => row.remove(), 180);
      document.getElementById('cartSubtotal').textContent = money(result.total);
      document.getElementById('cartGrandTotal').textContent = money(result.total);
      updateCartCount(result.count);
      const subtitle = document.querySelector('.cart-summary-row span');
      if (subtitle) subtitle.textContent = `Tạm tính (${result.count} món)`;
      if (result.count === 0) {
        document.getElementById('cartLayout').style.display = 'none';
        document.getElementById('cartEmptyState').style.display = '';
      }
      showToast('Đã xóa món khỏi giỏ hàng.');
    } catch (error) {
      button.disabled = false;
      showToast(error.message || 'Chưa xóa được món.');
    }
  }));

  try {
    const favorites = new Set(JSON.parse(localStorage.getItem('garan-favorites') || '[]').map(String));
    const saveFavorites = () => localStorage.setItem('garan-favorites', JSON.stringify([...favorites]));
    document.querySelectorAll('[data-favorite]').forEach(button => {
      const id = String(button.dataset.favorite);
      const paint = active => { button.classList.toggle('is-favorite', active); button.setAttribute('aria-pressed', String(active)); button.textContent = active ? '♥' : '♡'; button.setAttribute('aria-label', active ? 'Bỏ món yêu thích' : 'Lưu món yêu thích'); };
      paint(favorites.has(id));
      button.addEventListener('click', () => {
        favorites.has(id) ? favorites.delete(id) : favorites.add(id);
        paint(favorites.has(id)); saveFavorites();
        showToast(favorites.has(id) ? 'Đã lưu món yêu thích trên thiết bị này.' : 'Đã bỏ món khỏi danh sách yêu thích.');
      });
    });
  } catch { /* Storage may be disabled by the browser. */ }

  const revealItems = document.querySelectorAll('.reveal-on-scroll, .food-grid-col');
  if ('IntersectionObserver' in window && !window.matchMedia('(prefers-reduced-motion: reduce)').matches) {
    const observer = new IntersectionObserver(entries => entries.forEach(entry => {
      if (entry.isIntersecting) { entry.target.classList.add('is-visible'); observer.unobserve(entry.target); }
    }), { threshold: .08 });
    revealItems.forEach(item => { item.classList.add('will-reveal'); observer.observe(item); });
  }

  const header = document.querySelector('.site-header');
  const syncHeader = () => header?.classList.toggle('is-scrolled', window.scrollY > 12);
  syncHeader(); window.addEventListener('scroll', syncHeader, { passive: true });

  const carousel = document.querySelector('[data-review-carousel]');
  if (carousel) {
    const track = carousel.querySelector('.review-track');
    const cards = [...track.children];
    const dots = carousel.querySelector('[data-review-dots]');
    let active = 0;
    const buttons = cards.map((_, index) => {
      const dot = document.createElement('button'); dot.type = 'button'; dot.setAttribute('aria-label', `Xem đánh giá ${index + 1}`);
      dot.addEventListener('click', () => { active = index; paint(); }); dots.append(dot); return dot;
    });
    const paint = () => {
      track.style.transform = `translateX(-${active * 100}%)`;
      buttons.forEach((dot, index) => dot.classList.toggle('active', index === active));
    };
    carousel.querySelector('[data-review-prev]')?.addEventListener('click', () => { active = (active - 1 + cards.length) % cards.length; paint(); });
    carousel.querySelector('[data-review-next]')?.addEventListener('click', () => { active = (active + 1) % cards.length; paint(); });
    paint();
  }
})();
