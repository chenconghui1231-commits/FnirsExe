% NIfTI 文件查看器主程序
function nii_viewer_main()
    nii_file = 'E:\source\FnirsExe\FnirsExe\Resources\mni_icbm152_gm_tal_nlin_sym_09a.nii';
    
    % 检查文件是否存在
    if ~exist(nii_file, 'file')
        fprintf('文件不存在: %s\n', nii_file);
        return;
    end
    
    fprintf('=== NIfTI 文件查看器 ===\n');
    fprintf('文件: %s\n', nii_file);
    
    try
        % 使用手动读取函数
        [data, header] = read_nii_manual(nii_file);
        
        % 显示信息
        fprintf('\n=== 文件信息 ===\n');
        fprintf('数据维度: %dx%dx%d\n', size(data,1), size(data,2), size(data,3));
        fprintf('数据类型代码: %d\n', header.datatype);
        fprintf('位深度: %d\n', header.bitpix);
        fprintf('数据范围: %.6f 到 %.6f\n', min(data(:)), max(data(:)));
        
        % 显示图像
        show_nii_images(data);
        
    catch ME
        fprintf('❌ 读取失败: %s\n', ME.message);
        fprintf('尝试使用简单方法...\n');
        try_simple_method(nii_file);
    end
end

function show_nii_images(data)
    % 显示 NIfTI 图像的三个视图
    figure('Name', 'NIfTI 文件查看器', 'NumberTitle', 'off', ...
           'Position', [100 100 1200 800]);
    
    % 轴向视图
    subplot(2,3,1);
    axial_slice = round(size(data,3)/2);
    imagesc(data(:,:,axial_slice));
    axis image; colorbar;
    title(sprintf('轴向切片 (Z=%d)', axial_slice));
    colormap(jet);
    xlabel('X'); ylabel('Y');
    
    % 矢状视图
    subplot(2,3,2);
    sagittal_slice = round(size(data,1)/2);
    imagesc(squeeze(data(sagittal_slice,:,:))');
    axis image; colorbar;
    title(sprintf('矢状切片 (X=%d)', sagittal_slice));
    colormap(jet);
    xlabel('Y'); ylabel('Z');
    
    % 冠状视图
    subplot(2,3,3);
    coronal_slice = round(size(data,2)/2);
    imagesc(squeeze(data(:,coronal_slice,:))');
    axis image; colorbar;
    title(sprintf('冠状切片 (Y=%d)', coronal_slice));
    colormap(jet);
    xlabel('X'); ylabel('Z');
    
    % 数据直方图
    subplot(2,3,4);
    histogram(data(data > 0), 50);
    title('数据值分布 (大于0的值)');
    xlabel('数值'); ylabel('频次');
    grid on;
    
    % 最大强度投影
    subplot(2,3,5);
    mip = max(data, [], 3);
    imagesc(mip);
    axis image; colorbar;
    title('最大强度投影 (轴向)');
    colormap(jet);
    
    % 文本信息
    subplot(2,3,6);
    axis off;
    info_text = sprintf(['文件信息:\n' ...
                        '维度: %dx%dx%d\n' ...
                        '数据范围: [%.4f, %.4f]\n' ...
                        '非零体素: %d/%d\n' ...
                        '内存占用: %.1f MB'], ...
                        size(data,1), size(data,2), size(data,3), ...
                        min(data(:)), max(data(:)), ...
                        nnz(data), numel(data), ...
                        numel(data)*4/1024/1024);
    text(0.1, 0.9, info_text, 'VerticalAlignment', 'top', ...
         'FontSize', 10, 'FontName', 'FixedWidth');
    title('统计信息');
end

function try_simple_method(nii_file)
    % 尝试使用其他简单方法
    fprintf('尝试替代方法...\n');
    
    % 方法1: 尝试直接读取（如果文件是简单的二进制格式）
    try
        fid = fopen(nii_file, 'rb');
        if fid ~= -1
            % 跳过前 348 字节的头文件
            fseek(fid, 348, 'bof');
            % 尝试读取 100x100x100 的数据（假设是 float32）
            test_data = fread(fid, 100*100*100, 'float32');
            fclose(fid);
            
            if numel(test_data) == 1000000
                fprintf('✅ 可能成功读取了部分数据\n');
                % 这里可以继续处理...
            end
        end
    catch
        fprintf('简单方法也失败了\n');
    end
end