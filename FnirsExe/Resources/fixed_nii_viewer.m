% 修正版 NIfTI 查看器 - 修复版本
clear; clc; close all;

nii_file = 'E:\source\FnirsExe\FnirsExe\Resources\mni_icbm152_gm_tal_nlin_sym_09a.nii';

fprintf('=== 修正版 NIfTI 查看器 ===\n');
fprintf('文件: %s\n', nii_file);

% 检查文件是否存在
if ~exist(nii_file, 'file')
    error('❌ 文件不存在!');
end
fprintf('✅ 文件存在\n');

% 获取文件大小
file_info = dir(nii_file);
file_size = file_info.bytes;
fprintf('文件总大小: %d 字节 (%.1f MB)\n', file_size, file_size/1024/1024);

% 方法1: 直接读取整个文件（简化版）
try
    fprintf('\n正在读取文件数据...\n');
    
    % 由于头文件读取有问题，我们直接尝试标准MNI模板尺寸
    % MNI ICBM152 非对称模板的常见尺寸
    possible_dims = [
        181, 217, 181;    % 标准MNI尺寸
        256, 256, 256;    % 常见尺寸
        91, 109, 91;      % 半尺寸MNI
        362, 434, 362;    % 双尺寸MNI
        193, 229, 193     % 另一种MNI变体
    ];
    
    data_success = false;
    
    for i = 1:size(possible_dims, 1)
        test_dims = possible_dims(i, :);
        fprintf('尝试尺寸: %dx%dx%d... ', test_dims);
        
        % 尝试不同的数据类型
        data_types = {'uint8', 'int16', 'float32', 'float64'};
        
        for j = 1:length(data_types)
            try
                data = try_read_data(nii_file, test_dims, data_types{j});
                if is_valid_brain_data(data)
                    fprintf('✅ 成功! (数据类型: %s)\n', data_types{j});
                    data_success = true;
                    break;
                end
            catch
                % 继续尝试下一个数据类型
            end
        end
        
        if data_success
            break;
        else
            fprintf('❌ 失败\n');
        end
    end
    
    if data_success
        show_nii_results(data, test_dims);
    else
        % 如果标准尺寸都不行，尝试自动检测
        fprintf('\n尝试自动检测数据尺寸...\n');
        auto_detect_nii(nii_file, file_size);
    end
    
catch ME
    fprintf('❌ 读取失败: %s\n', ME.message);
end

function data = try_read_data(filename, dims, data_type)
    % 尝试读取数据
    fid = fopen(filename, 'rb');
    if fid == -1
        error('无法打开文件');
    end
    
    % 计算元素数量
    num_elements = prod(dims);
    
    % 根据数据类型确定字节大小
    switch data_type
        case 'uint8'
            bytes_per_element = 1;
        case 'int16'
            bytes_per_element = 2;
        case 'float32'
            bytes_per_element = 4;
        case 'float64'
            bytes_per_element = 8;
        otherwise
            bytes_per_element = 4;
    end
    
    % 尝试从文件开头读取（假设没有复杂头文件）
    data = fread(fid, num_elements, [data_type '=>float32']);
    
    % 如果数据量不够，尝试从其他位置读取
    if length(data) < num_elements
        fclose(fid);
        fid = fopen(filename, 'rb');
        
        % 尝试跳过可能的头文件（352字节是标准NIfTI偏移）
        fseek(fid, 352, 'bof');
        data = fread(fid, num_elements, [data_type '=>float32']);
    end
    
    fclose(fid);
    
    % 重塑数据维度
    if length(data) == num_elements
        data = reshape(data, dims);
    else
        error('数据尺寸不匹配');
    end
end

function valid = is_valid_brain_data(data)
    % 检查数据是否看起来像有效的脑部数据
    valid = false;
    
    if isempty(data) || numel(data) < 1000
        return;
    end
    
    % 检查数据范围（脑部数据通常在0-1或0-100之间）
    data_range = [min(data(:)), max(data(:))];
    
    % 有效的脑部数据特征：
    % 1. 有合理的数值范围
    % 2. 不是全零或全一
    % 3. 有一定的变化
    
    if data_range(2) - data_range(1) < 0.001
        return; % 变化太小
    end
    
    if data_range(2) > 1e6 || data_range(1) < -1e6
        return; % 数值范围不合理
    end
    
    valid = true;
end

function show_nii_results(data, dims)
    fprintf('\n=== 数据读取成功 ===\n');
    fprintf('数据维度: %dx%dx%d\n', size(data,1), size(data,2), size(data,3));
    fprintf('数据范围: %.6f 到 %.6f\n', min(data(:)), max(data(:)));
    fprintf('数据大小: %.1f MB\n', numel(data)*4/1024/1024);
    
    % 显示基本信息
    fprintf('非零体素: %d/%d (%.1f%%)\n', nnz(data), numel(data), nnz(data)/numel(data)*100);
    fprintf('平均值: %.4f, 标准差: %.4f\n', mean(data(:)), std(data(:)));
    
    % 显示图像
    figure('Name', 'NIfTI 查看器 - 脑部数据', 'NumberTitle', 'off', ...
           'Position', [100 100 1200 800]);
    
    % 轴向视图
    subplot(2,3,1);
    slice_num = round(size(data,3)/2);
    imagesc(data(:,:,slice_num));
    axis image; colorbar;
    title(sprintf('轴向切片 Z=%d/%d', slice_num, size(data,3)));
    colormap(jet);
    xlabel('X'); ylabel('Y');
    
    % 矢状视图
    subplot(2,3,2);
    slice_num = round(size(data,1)/2);
    imagesc(squeeze(data(slice_num,:,:))');
    axis image; colorbar;
    title(sprintf('矢状切片 X=%d/%d', slice_num, size(data,1)));
    colormap(jet);
    xlabel('Y'); ylabel('Z');
    
    % 冠状视图
    subplot(2,3,3);
    slice_num = round(size(data,2)/2);
    imagesc(squeeze(data(:,slice_num,:))');
    axis image; colorbar;
    title(sprintf('冠状切片 Y=%d/%d', slice_num, size(data,2)));
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
    mip_z = max(data, [], 3);
    imagesc(mip_z);
    axis image; colorbar;
    title('最大强度投影 (Z方向)');
    colormap(jet);
    
    % 三个方向的投影
    subplot(2,3,6);
    mip_combined = max(data, [], 3);
    imagesc(mip_combined);
    axis image; colorbar;
    title('三维最大强度投影');
    colormap(jet);
    
    fprintf('\n✅ 显示完成！\n');
    fprintf('图形窗口已显示脑部数据的三个正交视图\n');
end

function auto_detect_nii(filename, file_size)
    fprintf('自动检测数据格式...\n');
    
    % 计算可能的数据尺寸
    possible_sizes = [];
    
    % 常见的字节偏移量
    offsets = [0, 352, 540, 1024];
    
    for offset = offsets
        data_size = file_size - offset;
        
        % 尝试找到合理的三维尺寸
        for bytes_per_voxel = [1, 2, 4, 8]  % uint8, int16, float32, float64
            total_voxels = data_size / bytes_per_voxel;
            
            if total_voxels == round(total_voxels) && total_voxels > 1000
                % 寻找可能的三维分解
                possible_dims = factor(round(total_voxels));
                if length(possible_dims) >= 3
                    % 尝试组合成三维
                    dim1 = prod(possible_dims(1:floor(end/3)));
                    dim2 = prod(possible_dims(floor(end/3)+1:floor(2*end/3)));
                    dim3 = prod(possible_dims(floor(2*end/3)+1:end));
                    
                    if dim1 > 50 && dim2 > 50 && dim3 > 50
                        possible_sizes = [possible_sizes; offset, bytes_per_voxel, dim1, dim2, dim3];
                    end
                end
            end
        end
    end
    
    if isempty(possible_sizes)
        fprintf('❌ 无法自动检测数据格式\n');
        fprintf('建议使用专业的NIfTI查看软件\n');
        return;
    end
    
    fprintf('找到 %d 种可能的数据格式:\n', size(possible_sizes, 1));
    for i = 1:size(possible_sizes, 1)
        fprintf('  %d. 偏移量:%d字节, 类型:%d字节/体素, 尺寸:%dx%dx%d\n', ...
            i, possible_sizes(i,1), possible_sizes(i,2), ...
            possible_sizes(i,3), possible_sizes(i,4), possible_sizes(i,5));
    end
end