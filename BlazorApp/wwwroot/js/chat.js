// Funciones JavaScript para el chat RAG

// Manejo del input de texto
function setupTextareaAutoResize() {
    const textareas = document.querySelectorAll('.message-input');
    
    textareas.forEach(textarea => {
        textarea.addEventListener('input', function () {
            this.style.height = 'auto';
            this.style.height = (this.scrollHeight) + 'px';
        });
        
        // Ajustar altura inicial
        textarea.style.height = 'auto';
        textarea.style.height = (textarea.scrollHeight) + 'px';
    });
}

// Copiar código al portapapeles
function setupCodeCopyButtons() {
    document.addEventListener('click', function (e) {
        if (e.target.closest('.copy-code-btn')) {
            const button = e.target.closest('.copy-code-btn');
            const codeBlock = button.closest('.code-container').querySelector('code');
            const codeText = codeBlock.textContent;
            
            navigator.clipboard.writeText(codeText).then(() => {
                // Mostrar feedback visual
                const originalContent = button.innerHTML;
                button.innerHTML = '<i class="bi bi-check"></i>';
                button.style.backgroundColor = '#10b981';
                
                setTimeout(() => {
                    button.innerHTML = originalContent;
                    button.style.backgroundColor = '';
                }, 2000);
            }).catch(err => {
                console.error('Error al copiar código:', err);
            });
        }
    });
}

// Auto-scroll al final de los mensajes
function scrollToBottom(elementId = 'messages-container') {
    const container = document.getElementById(elementId) || 
                     document.querySelector('.messages-container');
    
    if (container) {
        container.scrollTop = container.scrollHeight;
    }
}

// Enfocar el input de mensaje
function focusInput() {
    const input = document.querySelector('.message-input');
    if (input) {
        input.focus();
    }
}

// Descargar archivo
function downloadFile(filename, content) {
    const blob = new Blob([content], { type: 'application/json' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = filename;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(url);
}

// Gestión de localStorage
function saveToLocalStorage(key, data) {
    try {
        localStorage.setItem(key, JSON.stringify(data));
    } catch (e) {
        console.warn('No se pudo guardar en localStorage:', e);
    }
}

function loadFromLocalStorage(key) {
    try {
        const data = localStorage.getItem(key);
        return data ? JSON.parse(data) : null;
    } catch (e) {
        console.warn('No se pudo cargar de localStorage:', e);
        return null;
    }
}

// Prevenir envío accidental con Enter en formularios
function preventFormSubmitOnEnter() {
    document.addEventListener('keydown', function (e) {
        if (e.key === 'Enter' && e.target.tagName === 'TEXTAREA' && !e.shiftKey) {
            e.stopPropagation();
        }
    });
}

// Manejo de archivos adjuntos
function setupFileUpload() {
    const fileInput = document.createElement('input');
    fileInput.type = 'file';
    fileInput.style.display = 'none';
    fileInput.accept = '.pdf,.docx,.txt,.md';
    fileInput.multiple = true;
    
    document.addEventListener('click', function (e) {
        if (e.target.closest('.btn-attach')) {
            fileInput.click();
        }
    });
    
    fileInput.addEventListener('change', function (e) {
        const files = Array.from(e.target.files);
        
        if (files.length > 0) {
            // Aquí podríamos enviar los archivos al servidor para procesamiento
            console.log('Archivos seleccionados:', files.map(f => f.name));
            
            // Resetear el input
            fileInput.value = '';
        }
    });
}

// Contador de caracteres en tiempo real
function setupCharacterCounter() {
    const textareas = document.querySelectorAll('.message-input');
    
    textareas.forEach(textarea => {
        const counter = textarea.closest('.input-container').querySelector('.char-count');
        
        textarea.addEventListener('input', function () {
            if (counter) {
                counter.textContent = `${this.value.length} / 2000`;
                
                // Cambiar color si se acerca al límite
                if (this.value.length > 1800) {
                    counter.style.color = '#f56565';
                } else if (this.value.length > 1500) {
                    counter.style.color = '#ed8936';
                } else {
                    counter.style.color = '';
                }
            }
        });
    });
}

// Efectos de animación para mensajes nuevos
function animateNewMessage(messageElement) {
    if (!messageElement) return;
    
    messageElement.style.animation = 'none';
    setTimeout(() => {
        messageElement.style.animation = 'fadeIn 0.3s ease-out';
    }, 10);
}

// Notificaciones del sistema
function showNotification(message, type = 'info') {
    const notification = document.createElement('div');
    notification.className = `notification notification-${type}`;
    notification.innerHTML = `
        <div class="notification-content">
            <i class="bi bi-${getNotificationIcon(type)}"></i>
            <span>${message}</span>
        </div>
        <button class="notification-close" onclick="this.parentElement.remove()">
            <i class="bi bi-x"></i>
        </button>
    `;
    
    document.body.appendChild(notification);
    
    // Auto-remove después de 5 segundos
    setTimeout(() => {
        if (notification.parentNode) {
            notification.remove();
        }
    }, 5000);
}

function getNotificationIcon(type) {
    switch (type) {
        case 'success': return 'check-circle';
        case 'error': return 'exclamation-circle';
        case 'warning': return 'exclamation-triangle';
        default: return 'info-circle';
    }
}

// Estilos CSS para notificaciones
function addNotificationStyles() {
    if (!document.querySelector('#notification-styles')) {
        const style = document.createElement('style');
        style.id = 'notification-styles';
        style.textContent = `
            .notification {
                position: fixed;
                top: 20px;
                right: 20px;
                background: white;
                border-radius: 12px;
                padding: 16px;
                box-shadow: 0 8px 32px rgba(0, 0, 0, 0.15);
                display: flex;
                align-items: center;
                justify-content: space-between;
                gap: 12px;
                min-width: 300px;
                max-width: 400px;
                z-index: 9999;
                animation: slideInRight 0.3s ease-out;
                border-left: 4px solid #4299e1;
            }
            
            .notification-success {
                border-left-color: #38a169;
            }
            
            .notification-error {
                border-left-color: #e53e3e;
            }
            
            .notification-warning {
                border-left-color: #dd6b20;
            }
            
            .notification-content {
                display: flex;
                align-items: center;
                gap: 12px;
                flex: 1;
            }
            
            .notification-content i {
                font-size: 1.25rem;
            }
            
            .notification-success .notification-content i {
                color: #38a169;
            }
            
            .notification-error .notification-content i {
                color: #e53e3e;
            }
            
            .notification-warning .notification-content i {
                color: #dd6b20;
            }
            
            .notification-close {
                background: none;
                border: none;
                color: #a0aec0;
                cursor: pointer;
                padding: 4px;
                border-radius: 4px;
                transition: all 0.2s ease;
            }
            
            .notification-close:hover {
                background: #f7fafc;
                color: #718096;
            }
            
            @keyframes slideInRight {
                from {
                    transform: translateX(100%);
                    opacity: 0;
                }
                to {
                    transform: translateX(0);
                    opacity: 1;
                }
            }
            
            @media (prefers-color-scheme: dark) {
                .notification {
                    background: #2d3748;
                    color: #e2e8f0;
                }
                
                .notification-close:hover {
                    background: #4a5568;
                    color: #cbd5e0;
                }
            }
        `;
        document.head.appendChild(style);
    }
}

// Inicializar todas las funciones
function initializeChatFeatures() {
    setupTextareaAutoResize();
    setupCodeCopyButtons();
    preventFormSubmitOnEnter();
    setupFileUpload();
    setupCharacterCounter();
    addNotificationStyles();
    
    // Hacer scroll al cargar
    setTimeout(() => {
        scrollToBottom();
        focusInput();
    }, 100);
    
    console.log('Funciones de chat inicializadas correctamente');
}

// Copy Markdown to clipboard
async function copyMarkdownToClipboard(element) {
    try {
        if (element && element.domElement) {
            const text = element.domElement.innerText || element.domElement.textContent;
            await navigator.clipboard.writeText(text);
            console.log('Markdown copied to clipboard');
        }
    } catch (err) {
        console.error('Error copying Markdown:', err);
    }
}

// Print Markdown content
async function printMarkdownContent(element) {
    try {
        if (element && element.domElement) {
            const printContent = element.domElement.innerHTML;
            const printWindow = window.open('', '_blank');
            
            if (printWindow) {
                printWindow.document.write(`
                    <!DOCTYPE html>
                    <html>
                    <head>
                        <title>Imprimir contenido</title>
                        <style>
                            body { font-family: Arial, sans-serif; padding: 20px; max-width: 800px; margin: 0 auto; }
                            h1, h2, h3 { color: #2d3748; }
                            code { background: #f7fafc; padding: 2px 6px; border-radius: 4px; font-family: monospace; }
                            pre { background: #2d3748; color: #f7fafc; padding: 16px; border-radius: 8px; overflow-x: auto; }
                            blockquote { border-left: 4px solid #4299e1; margin: 0; padding-left: 16px; color: #718096; }
                            table { border-collapse: collapse; width: 100%; margin: 16px 0; }
                            th, td { border: 1px solid #cbd5e0; padding: 8px 12px; text-align: left; }
                            th { background: #edf2f7; font-weight: bold; }
                            ul, ol { margin: 16px 0; padding-left: 24px; }
                            li { margin: 4px 0; }
                        </style>
                    </head>
                    <body>
                        ${printContent}
                        <script>
                            window.onload = function() {
                                window.print();
                                window.onafterprint = function() {
                                    window.close();
                                };
                            };
                        <\/script>
                    </body>
                    </html>
                `);
                printWindow.document.close();
            }
        }
    } catch (err) {
        console.error('Error printing Markdown:', err);
    }
}

// Exportar funciones al scope global
window.chatFunctions = {
    scrollToBottom,
    focusInput,
    downloadFile,
    saveToLocalStorage,
    loadFromLocalStorage,
    showNotification,
    animateNewMessage,
    initializeChatFeatures
};

window.markdownRenderer = {
    copyMarkdownToClipboard,
    printMarkdownContent
};

// Inicializar cuando el DOM esté listo
if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', initializeChatFeatures);
} else {
    initializeChatFeatures();
}